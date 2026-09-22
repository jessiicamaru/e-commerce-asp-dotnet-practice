#!/usr/bin/env python3
"""Fills the catalogue with real cameras, through the API, as an administrator would.

    cd server
    ADMIN_EMAIL=... ADMIN_PASSWORD=... python seed/seed-catalogue.py

Why through the gateway rather than SQL: every row this writes goes down the same path a person
uses, so seeding exercises validation, the outbox, the availability announcements and the price
rules rather than stepping around them. A seeder that writes straight to the database is the one
place a catalogue can hold something the API would have refused - and this one found a real gap on
its first run, because it is the first API client that ever tried to translate a variant option.

**It is idempotent by SKU.** Running it twice adds nothing; running it after a failure finishes the
job. Translations, dollar prices and stock are upserts, so they are rewritten either way - that is
cheap and it makes a half-finished run recoverable.

**It never deletes.** What is already in the catalogue is not this script's to remove, and there is
no endpoint that would let it.

The data, and a warning about the prices, is in cameras.json beside this file.
"""

import json
import os
import sys
import time
import urllib.error
import urllib.request

BASE = os.environ.get("GATEWAY_URL", "http://localhost:5000").rstrip("/")
HERE = os.path.dirname(os.path.abspath(__file__))

GREEN, YELLOW, RED, DIM, RESET = "\033[32m", "\033[33m", "\033[31m", "\033[2m", "\033[0m"


def say(mark, colour, message):
    print(f"  {colour}{mark}{RESET}  {message}")


def ok(message):
    say("ok", GREEN, message)


def skip(message):
    say("--", DIM, message)


def warn(message):
    say("!!", YELLOW, message)


def die(message):
    say("!!", RED, message)
    sys.exit(1)


def set_stock(variant_id, quantity, token):
    """Stock for one variant, waiting for Inventory to have heard of it.

    **Inventory learns about a variant through the broker, not through this call.** Catalog stages
    the event in its outbox and commits; Inventory consumes it and registers the variant a moment
    later. A stock write sent in between is a 404 about a variant that certainly exists - which is
    correct of Inventory, and is why this waits rather than treating it as an error.

    Seen on the second seeding run and not the third: the timing depends on how busy the broker is,
    which is exactly the kind of thing that passes locally and fails in CI.
    """
    for _ in range(20):
        answer = call(
            "PUT", f"/api/stock/{variant_id}",
            {"quantityOnHand": quantity}, token, tolerate=(404,))

        if answer is not None:
            return answer

        time.sleep(0.5)

    die(f"Inventory never registered variant {variant_id}. Is it running, and is RabbitMQ up?")


def call(method, path, body=None, token=None, tolerate=()):
    request = urllib.request.Request(
        BASE + path,
        method=method,
        data=None if body is None else json.dumps(body).encode("utf-8"),
        headers={
            "Content-Type": "application/json",
            "Accept": "application/json",
            # Seeded text is the DEFAULT language and the DEFAULT currency; the English text and the
            # dollar prices are written afterwards, each in its own request (specs/021, specs/022).
            "Accept-Language": "vi",
            "X-Currency": "VND",
            **({"Authorization": "Bearer " + token} if token else {}),
        },
    )

    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            payload = response.read()
            return json.loads(payload) if payload else None
    except urllib.error.HTTPError as error:
        detail = error.read().decode("utf-8", "replace")

        # A status the caller said it would handle - today, only Inventory's 404 for a variant it
        # has not been told about yet.
        if error.code in tolerate:
            return None

        die(f"{method} {path} -> {error.code}\n    {detail[:400]}")
    except urllib.error.URLError as error:
        die(f"{BASE} is not answering ({error.reason}). Is the stack up?")


def sign_in():
    email = os.environ.get("ADMIN_EMAIL")
    password = os.environ.get("ADMIN_PASSWORD")

    if not email or not password:
        die("Set ADMIN_EMAIL and ADMIN_PASSWORD (they are in server/.env).")

    answer = call("POST", "/api/auth/login", {"email": email, "password": password})
    token = (answer or {}).get("token") or (answer or {}).get("accessToken")

    if not token:
        die("The administrator could not sign in.")

    return token


def every_product_sku():
    """Every product SKU already in the catalogue, mapped to its id."""
    skus = {}
    page = 1

    while True:
        answer = call("GET", f"/api/products?pageNumber={page}&pageSize=100") or {}

        for item in answer.get("items") or []:
            skus[item["sku"]] = item["id"]

        if not answer.get("hasNextPage"):
            return skus

        page += 1


def seed_categories(data, token):
    listed = call("GET", "/api/categories") or []
    rows = listed.get("items") if isinstance(listed, dict) else listed
    by_slug = {row["slug"]: row["id"] for row in rows}
    categories = {}

    for category in data["categories"]:
        if category["slug"] in by_slug:
            categories[category["slug"]] = by_slug[category["slug"]]
            skip(f"category {category['slug']} is already there")
            continue

        created = call("POST", "/api/categories", {
            "name": category["name"],
            "description": category.get("description"),
            "slug": category["slug"],
            "parentCategoryId": None,
        }, token)
        categories[category["slug"]] = created["id"]
        ok(f"category {category['slug']}")

    # The English names, written whether the category is new or not - an upsert, and cheap. Category
    # text went untranslated until specs/026, and a shop reading English showed Vietnamese category
    # names under every product card.
    for category in data["categories"]:
        call("PUT", f"/api/categories/{categories[category['slug']]}/translations/en", {
            "name": category["en"]["name"],
            "description": category["en"].get("description"),
        }, token)

    return categories


def seed_variant(product_id, variant, present, is_first, token):
    """Returns the variant's id, creating it unless it is already there."""
    if variant["sku"] in present:
        return present[variant["sku"]]

    if is_first:
        # The first variant is created WITH the product and reuses the product's id (specs/020).
        # It carries the product's sku, not its own, so it is never in `present` by this sku - and
        # adding a second variant here would give the product a duplicate shape.
        return product_id

    made = call("POST", f"/api/products/{product_id}/variants", {
        "sku": variant["sku"],
        "price": variant["vnd"],
        "options": [{"name": o["vi"][0], "value": o["vi"][1]} for o in variant["options"]],
    }, token)
    ok(f"    variant {variant['sku']}")
    return made["id"]


def translate_options(product_id, variant_id, wanted, detail, token):
    """The options of this variant, in English.

    The option ids come off the product read back rather than being guessed, which is the whole
    reason `VariantOptionResponse` carries one: before that it did not, and this endpoint could not
    be called by anything outside the database.

    ⚠️ **Matched by name, never by position.** The first version of this paired `stored[i]` with
    `wanted[i]`, and the response does not come back in the order the options were entered - so
    `Bộ: Chỉ thân máy` was given the English `Colour: Black`. The database said so plainly and the
    storefront would have shown it. A list of things with identities is never matched by index.
    """
    this = next((v for v in detail.get("variants") or [] if v["id"] == variant_id), None)

    if this is None:
        return

    english = {option["vi"][0]: option["en"] for option in wanted}

    for option in this.get("options") or []:
        pair = english.get(option["name"])

        if pair is None:
            warn(f"    no English for option '{option['name']}' on {this['sku']}")
            continue

        call("PUT", f"/api/products/{product_id}/options/{option['id']}/translations/en",
             {"name": pair[0], "value": pair[1]}, token)


def main():
    with open(os.path.join(HERE, "cameras.json"), encoding="utf-8") as handle:
        data = json.load(handle)

    print(f"\nSeeding the catalogue through {BASE}\n")
    warn("The prices in cameras.json are APPROXIMATE - see the note at the top of that file.")
    print()

    token = sign_in()
    ok("administrator signed in")

    categories = seed_categories(data, token)
    known = every_product_sku()
    added = skipped = 0

    for product in data["products"]:
        first = product["variants"][0]

        if product["sku"] in known:
            product_id = known[product["sku"]]
            skip(f"{product['vi']['name']} is already there")
            skipped += 1
        else:
            # The product carries the DEFAULT language's text and the DEFAULT currency's price, and
            # its first variant's options come with it.
            created = call("POST", "/api/products", {
                "name": product["vi"]["name"],
                "description": product["vi"]["description"],
                "price": first["vnd"],
                "sku": product["sku"],
                "categoryId": categories[product["category"]],
                "options": [{"name": o["vi"][0], "value": o["vi"][1]} for o in first["options"]],
            }, token)
            product_id = created["id"]
            added += 1
            ok(product["vi"]["name"])

        # The English text. The Vietnamese columns are the fallback, so this is purely additive.
        call("PUT", f"/api/products/{product_id}/translations/en", {
            "name": product["en"]["name"],
            "description": product["en"]["description"],
        }, token)

        detail = call("GET", f"/api/products/{product_id}")
        present = {v["sku"]: v["id"] for v in detail.get("variants") or []}

        # The id each variant ended up with, kept rather than worked out twice: asking
        # `seed_variant` again after the first pass would try to CREATE the ones it just created,
        # because `present` was read before them.
        seeded = []

        for index, variant in enumerate(product["variants"]):
            variant_id = seed_variant(product_id, variant, present, index == 0, token)
            seeded.append((variant, variant_id))

            # A price somebody DECIDED, never a conversion (specs/022).
            call("PUT", f"/api/products/{product_id}/variants/{variant_id}/prices/USD",
                 {"amount": variant["usd"]}, token)

            # Stock, or the storefront shows a catalogue nobody can buy from.
            set_stock(variant_id, variant["stock"], token)

        # Read back once more: the loop above may have added variants, and the option ids are only
        # in the response.
        detail = call("GET", f"/api/products/{product_id}")

        for variant, variant_id in seeded:
            translate_options(product_id, variant_id, variant["options"], detail, token)

    print()
    ok(f"{added} product(s) added, {skipped} already there")
    print()
    warn("No images: there is no honest way to obtain product photographs here, and a placeholder")
    warn("that looks like a photograph is worse than a blank. Products read back with imageUrl null.")
    print()


if __name__ == "__main__":
    main()
