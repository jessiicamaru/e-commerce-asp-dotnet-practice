# Quickstart: Validating the Storefront Redesign

> Written on 2026-09-27, after the feature merged (#62), from the code at that merge, the pull request and docs/architecture/storefront.md.

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

The visual scenarios are checked by looking; at this merge the client had no unit tests and no browser
automation. The results recorded come from the PR.

---

## Prerequisites

```bash
cd server
docker compose up -d
./start-dev.sh                 # the services and the gateway on :5000
ADMIN_EMAIL=... ADMIN_PASSWORD=... python seed/seed-catalogue.py   # optional: the 14 cameras

cd ../client
npm ci
npm run dev                    # http://localhost:5173, /api proxied to :5000
```

---

## Scenario 1 - The static checks (SC-005)

```bash
cd client
npm run lint                   # oxlint
npm run build                  # tsc -b && vite build
```

**Expected**: both clean. **At the merge**: type-check, lint and build all clean (PR). There was no
`npm test` yet.

---

## Scenario 2 - The landing view (User Story 1, FR-002, FR-003)

Open `http://localhost:5173/` at a desktop width.

**Expected**: warm off-white page, lime accent; a hero with the heading, "Browse everything", the product
count, up to six category chips, and the dearest product on the page inside the panel - not repeated as
the first tile below. Search for anything, or pick a category, or go to page 2: the hero disappears.

---

## Scenario 3 - Narrow widths (SC-001)

Resize to 500px. **Expected**: no horizontal scroll; the bar wraps and becomes a rounded rectangle, not a
stadium.

**At the merge**: screenshots were read at 1440px, 500px and on the product page, using headless Chrome.
⚠️ **Below 500px is not verified**: headless Chrome has a 500px minimum layout width. A probe for the
apparent 430px overflow reported `VIEW=500 SCROLL=500`; there was none.

To probe for overflow yourself, run in the browser console:

```js
[document.documentElement.clientWidth, document.documentElement.scrollWidth]
```

**Expected**: the two numbers are equal.

---

## Scenario 4 - Search from any page (User Story 2)

On a product page, type `canon` in the bar and press Enter. **Expected**: the address becomes `/?q=canon`
and the grid shows matching products. Signed in with items in the cart, the cart link shows a count;
signed out, the network panel shows no `/api/cart` request.

---

## Scenario 5 - Placeholders and variant cards (User Stories 3, 4)

With products that have no image, the grid shows aperture tiles in varied pale tints; reload and each
product keeps its tint. On a product with two variants, click anywhere on a variant card: it is selected
and highlighted; Tab and the arrow keys still move between the radios. Every card and product page reads
"Sold by The shop".

---

## Scenario 6 - Delete a category (User Story 5, FR-008, SC-004)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~DeleteCategoryTests"
```

**Expected**: 4 pass - an empty category is removed; one with a product is refused with its name and
"1 product"; emptying it makes it removable; a missing one is 404. **At the merge**: 255 tests across the
six projects, 4 new.

Through the gateway:

```bash
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
curl -s -o /dev/null -w '%{http_code}\n' -X DELETE http://localhost:5000/api/categories/<empty-id> -H "Authorization: Bearer $ADMIN"      # 204
curl -s -X DELETE http://localhost:5000/api/categories/<id-with-products> -H "Authorization: Bearer $ADMIN"                          # 409
```

---

## Scenario 7 - The cleaner removes unused categories (FR-009, SC-003)

```bash
cd server
python seed/clean-test-debris.py --yes
```

**Expected**: after the products, a line `ok  <n> of <m> unused categor(ies) removed`; categories still
holding products are refused by the API and kept. **At the merge**: 93 junk categories removed. On a
Windows console the script now prints Vietnamese names instead of crashing.
