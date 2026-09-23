"""Put the photographs in seed/images/ onto the products that match them by SKU.

    cd server
    ADMIN_EMAIL=... ADMIN_PASSWORD=... python seed/seed-images.py          # says what it would do
    ADMIN_EMAIL=... ADMIN_PASSWORD=... python seed/seed-images.py --yes    # does it

`seed/images/` is **gitignored on purpose**. This repository is public and a product
photograph belongs to whoever took it; the files are staged there and uploaded to the
`catalog_images` volume, which is local. Nothing here commits an image, and nothing here
fetches one - where they come from is your business, and licensing is a real question rather
than a formality.

Match is by **file name = SKU**: `SONY-A7M4.jpg` finds the product whose sku is `SONY-A7M4`.
The extension is ignored, because Catalog decides the type from the bytes (specs/019) and so
does this script - a `.jpg` holding a PNG uploads fine, and an SVG renamed `.png` is refused
here rather than at the server.

Idempotent in the only sense that matters: uploading again replaces the image, and Catalog
writes the new file, switches the row, then deletes the old one, so a re-run never leaves the
product pointing at nothing.
"""
import os
import pathlib
import sys
import urllib.error
import urllib.request
import uuid
import json

sys.stdout.reconfigure(encoding="utf-8")

BASE = os.environ.get("GATEWAY_URL", "http://localhost:5000") + "/api"
IMAGES = pathlib.Path(__file__).resolve().parent / "images"
MAX_BYTES = 2 * 1024 * 1024

GREEN, YELLOW, RED, DIM, OFF = "\033[32m", "\033[33m", "\033[31m", "\033[2m", "\033[0m"

# The three Catalog accepts, recognised the way Catalog recognises them: by content.
MAGIC = {b"\xff\xd8\xff": "image/jpeg", b"\x89PNG\r\n\x1a\n": "image/png"}


def kind(content: bytes) -> str | None:
    for magic, mime in MAGIC.items():
        if content.startswith(magic):
            return mime
    if content[:4] == b"RIFF" and content[8:12] == b"WEBP":
        return "image/webp"
    return None


def call(method, path, body=None, token=None, raw=None, content_type=None):
    data = raw if raw is not None else (json.dumps(body).encode() if body is not None else None)
    request = urllib.request.Request(BASE + path, data=data, method=method)
    request.add_header("Content-Type", content_type or "application/json")
    if token:
        request.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.read()


def detail(body: bytes) -> str:
    try:
        return json.loads(body).get("detail") or json.loads(body).get("title") or ""
    except Exception:
        return body.decode("utf-8", "replace")[:160]


def main() -> int:
    commit = "--yes" in sys.argv
    email, password = os.environ.get("ADMIN_EMAIL"), os.environ.get("ADMIN_PASSWORD")
    if not email or not password:
        print(f"{RED}ADMIN_EMAIL and ADMIN_PASSWORD must be set.{OFF}")
        return 2

    if not IMAGES.is_dir():
        print(f"{RED}No {IMAGES}{OFF}")
        return 2

    files = {f.stem.upper(): f for f in sorted(IMAGES.iterdir())
             if f.is_file() and not f.name.startswith(".")}
    if not files:
        print(f"{YELLOW}No images in {IMAGES}. Name them after the SKU: SONY-A7M4.jpg{OFF}")
        return 0

    status, body = call("POST", "/auth/login", {"email": email, "password": password})
    if status != 200:
        print(f"{RED}Could not sign in ({status}): {detail(body)}{OFF}")
        return 1
    token = json.loads(body)["token"]

    status, body = call("GET", "/products?pageSize=200")
    if status != 200:
        print(f"{RED}Could not read the catalogue ({status}).{OFF}")
        return 1
    products = {p["sku"].upper(): p for p in json.loads(body)["items"]}

    print(f"{len(files)} image(s), {len(products)} product(s)"
          + ("" if commit else f"  {DIM}(dry run - pass --yes to upload){OFF}") + "\n")

    uploaded = skipped = failed = 0
    for sku, path in files.items():
        content = path.read_bytes()
        product = products.get(sku)

        if product is None:
            print(f"  {YELLOW}--{OFF} {path.name:<22} no product with sku {sku}")
            skipped += 1
            continue

        mime = kind(content)
        if mime is None:
            print(f"  {RED}!!{OFF} {path.name:<22} not a JPEG, PNG or WebP by its bytes")
            failed += 1
            continue

        if len(content) > MAX_BYTES:
            print(f"  {RED}!!{OFF} {path.name:<22} {len(content)//1024}KB is over Catalog's 2MB limit")
            failed += 1
            continue

        had = "replaces the current image" if product["imageUrl"] else "first image"
        if not commit:
            print(f"  {DIM}would upload{OFF} {path.name:<22} -> {product['name'][:30]:<32} "
                  f"{DIM}{len(content)//1024}KB {mime}, {had}{OFF}")
            uploaded += 1
            continue

        boundary = "----seed" + uuid.uuid4().hex
        multipart = (
            f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{path.name}"\r\n'
            f"Content-Type: {mime}\r\n\r\n".encode() + content
            + f"\r\n--{boundary}--\r\n".encode()
        )
        status, body = call("PUT", f"/products/{product['id']}/image", raw=multipart, token=token,
                            content_type=f"multipart/form-data; boundary={boundary}")
        if status == 200:
            print(f"  {GREEN}ok{OFF} {path.name:<22} -> {product['name'][:30]:<32} "
                  f"{DIM}{len(content)//1024}KB, {had}{OFF}")
            uploaded += 1
        else:
            print(f"  {RED}!!{OFF} {path.name:<22} {status}: {detail(body)}")
            failed += 1

    missing = sorted(set(products) - set(files))
    if missing:
        print(f"\n{YELLOW}{len(missing)} product(s) still without a file here:{OFF} "
              + ", ".join(missing))

    verb = "uploaded" if commit else "would upload"
    print(f"\n{GREEN}{uploaded} {verb}{OFF}, {skipped} skipped, "
          + (f"{RED}{failed} failed{OFF}" if failed else "0 failed"))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
