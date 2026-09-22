#!/usr/bin/env python3
"""Removes from the catalogue everything that is not a seeded camera.

    cd server
    ADMIN_EMAIL=... ADMIN_PASSWORD=... python seed/clean-test-debris.py          # says what it would do
    ADMIN_EMAIL=... ADMIN_PASSWORD=... python seed/clean-test-debris.py --yes    # does it

`verify-saga.sh`, `verify-auth.sh` and the Bruno collection each create a real product every time
they run, and none of them clean up - so a catalogue somebody has been testing against fills with
`E2E Widget 17900854702164` and `iPhone 16 Pro Max 1790085352812`. There were ninety-four of them
against fourteen real cameras, and a listing sorted cheapest-first was a wall of $0.01 widgets.

**It keeps what cameras.json names and deletes the rest**, which is the safe way round: a list of
things to keep cannot quietly miss a new kind of debris, and a list of patterns to delete can.

It goes through the API, so each deletion is the same Admin operation a person would perform, is
announced to Inventory, and is refused if the caller is not an administrator. It prints what it is
about to do and does nothing without `--yes`, because this is the one operation here that cannot be
undone.

⚠️ **Only ever point this at a development catalogue.** It deletes products.
"""

import json
import os
import sys
import urllib.error
import urllib.request

BASE = os.environ.get("GATEWAY_URL", "http://localhost:5000").rstrip("/")
HERE = os.path.dirname(os.path.abspath(__file__))

GREEN, YELLOW, RED, DIM, RESET = "\033[32m", "\033[33m", "\033[31m", "\033[2m", "\033[0m"


def call(method, path, body=None, token=None):
    request = urllib.request.Request(
        BASE + path,
        method=method,
        data=None if body is None else json.dumps(body).encode("utf-8"),
        headers={
            "Content-Type": "application/json",
            "Accept": "application/json",
            **({"Authorization": "Bearer " + token} if token else {}),
        },
    )

    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            payload = response.read()
            # True rather than None for an empty body: a 204 from DELETE and a failure must not look
            # the same to the caller, or the count at the end is a count of attempts.
            return json.loads(payload) if payload else True
    except urllib.error.HTTPError as error:
        detail = error.read().decode("utf-8", "replace")
        print(f"  {RED}!!{RESET}  {method} {path} -> {error.code}\n      {detail[:200]}")
        return None
    except urllib.error.URLError as error:
        sys.exit(f"  {RED}!!{RESET}  {BASE} is not answering ({error.reason}). Is the stack up?")


def main():
    confirmed = "--yes" in sys.argv

    with open(os.path.join(HERE, "cameras.json"), encoding="utf-8") as handle:
        keep = {product["sku"] for product in json.load(handle)["products"]}

    email = os.environ.get("ADMIN_EMAIL")
    password = os.environ.get("ADMIN_PASSWORD")

    if not email or not password:
        sys.exit("Set ADMIN_EMAIL and ADMIN_PASSWORD (they are in server/.env).")

    answer = call("POST", "/api/auth/login", {"email": email, "password": password}) or {}
    token = answer.get("token") or answer.get("accessToken")

    if not token:
        sys.exit("The administrator could not sign in.")

    doomed = []
    page = 1

    while True:
        listing = call("GET", f"/api/products?pageNumber={page}&pageSize=100") or {}

        for item in listing.get("items") or []:
            if item["sku"] not in keep:
                doomed.append(item)

        if not listing.get("hasNextPage"):
            break

        page += 1

    print(f"\n{len(keep)} camera(s) to keep, {len(doomed)} product(s) to delete.\n")

    for item in doomed[:10]:
        print(f"  {DIM}-{RESET}  {item['sku']:<28} {item['name'][:50]}")

    if len(doomed) > 10:
        print(f"  {DIM}…and {len(doomed) - 10} more{RESET}")

    if not doomed:
        print(f"\n  {GREEN}ok{RESET}  nothing to clean\n")
        return

    if not confirmed:
        print(f"\n  {YELLOW}!!{RESET}  Nothing was deleted. Re-run with --yes to do it.\n")
        return

    print()
    deleted = 0

    failed = 0

    for item in doomed:
        if call("DELETE", f"/api/products/{item['id']}", token=token) is None:
            failed += 1
        else:
            deleted += 1

    remaining = call("GET", "/api/products?pageSize=1") or {}
    print(f"\n  {GREEN}ok{RESET}  {deleted} deleted; {remaining.get('totalCount', '?')} product(s) left")

    if failed:
        print(f"  {RED}!!{RESET}  {failed} could not be deleted - the errors are above\n")
    else:
        print()


if __name__ == "__main__":
    main()
