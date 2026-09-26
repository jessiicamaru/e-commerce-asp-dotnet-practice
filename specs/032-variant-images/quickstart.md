# Quickstart: Validating variant photographs

> Written on 2026-09-27, after the feature merged (#73), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/api.md](contracts/api.md)

---

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # or ./start-dev.sh
```

You need two seller accounts, seller A owning a product with **at least two variants**, and the
product listed and approved. Since this merged, a seller is approved by staff (specs/044, with a
confirmed address, specs/063) and a seller's product waits for review (specs/045) - and a seller
**changing a variant photograph sends an approved product back to review** (specs/045), so re-approve it
as an administrator after scenario 1 before checking the storefront. Bruno's `seller` folder does all
of the account work.

```bash
TOKEN_A=...; TOKEN_B=...; PRODUCT=...     # seller A's product
VARIANT=$(curl -s http://localhost:5000/api/products/$PRODUCT | jq -r '.variants[1].id')
OTHER=$(curl -s http://localhost:5000/api/products/$PRODUCT | jq -r '.variants[0].id')
```

Any JPEG, PNG or WebP under 2 MB will do as `silver.jpg`.

---

## Scenario 1 — A seller photographs one shape (US2.1)

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X PUT \
  http://localhost:5000/api/products/$PRODUCT/variants/$VARIANT/image \
  -H "Authorization: Bearer $TOKEN_A" -F file=@silver.jpg
curl -s http://localhost:5000/api/products/$PRODUCT -H "Authorization: Bearer $TOKEN_A" \
  | jq '.imageUrl, (.variants[] | {sku, imageUrl})'
```

**Expected**: `204`. The photographed variant's `imageUrl` is its **own** address
(`/api/products/{id}/variants/{variantId}/image?v=…`); every other variant's is the product's address;
`.imageUrl` of the product itself is unchanged (US4). If the product has no photograph, the others are
`null`.

## Scenario 2 — Another seller is refused with 404 (US2.2, SC-002)

```bash
curl -s -X PUT http://localhost:5000/api/products/$PRODUCT/variants/$VARIANT/image \
  -H "Authorization: Bearer $TOKEN_B" -F file=@silver.jpg | jq .detail
curl -s -X DELETE http://localhost:5000/api/products/$PRODUCT/variants/$VARIANT/image \
  -H "Authorization: Bearer $TOKEN_B" | jq .detail
curl -s -X PUT http://localhost:5000/api/products/$PRODUCT/variants/$(uuidgen)/image \
  -H "Authorization: Bearer $TOKEN_B" -F file=@silver.jpg | jq .detail
```

**Expected**: all three `404`, all worded `Variant with ID '…' was not found.` - "not yours" and "does
not exist" indistinguishable.

## Scenario 3 — The bytes decide, and the size is capped (US2.3)

```bash
echo '<svg xmlns="http://www.w3.org/2000/svg"/>' > fake.jpg
curl -s -X PUT http://localhost:5000/api/products/$PRODUCT/variants/$VARIANT/image \
  -H "Authorization: Bearer $TOKEN_A" -F file=@fake.jpg | jq .errors
```

**Expected**: `400`, `errors.File` = `The file is not a JPEG, PNG or WebP image.` A file over 2 MB is
`400` `The image is larger than 2 MB.`

## Scenario 4 — Removing falls back to the product (US1.2)

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X DELETE \
  http://localhost:5000/api/products/$PRODUCT/variants/$VARIANT/image -H "Authorization: Bearer $TOKEN_A"
```

**Expected**: `204`, and the variant's `imageUrl` is the product's again. Removing again is `204` too.

## Scenario 5 — The picture follows the chooser (US1, SC-001)

Photograph one variant again, then open the product page in the storefront and click each variant.

**Expected**: the picture changes to the photographed shape's and back to the product's for the others;
the catalogue grid card shows the product's photograph throughout. This is what the pull request's
screenshot showed; a unit test cannot see it.

## Scenario 6 — The automated tests (SC-003, Principle V)

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Catalog.Tests \
  --filter "FullyQualifiedName~ProductImageTests|FullyQualifiedName~SellerOwnershipTests"
cd ../client && npm test -- src/pages/product src/pages/shop-product
```

**Expected**: passing, including `A_product_and_its_first_variant_never_name_the_same_file`,
`Deleting_a_product_deletes_every_variants_image_too` (seen red before its fix, per the pull request),
`Replacing_a_variants_photograph_leaves_one_file`, `A_variant_photograph_is_recognised_by_its_bytes`,
the two fallback tests and `A_seller_cannot_photograph_another_sellers_variant`. At the merge Catalog
stood at 122 tests and the client at 43.

## Scenario 7 — Nothing left behind (US3)

As an administrator, delete the product (`DELETE /api/products/$PRODUCT`), then ask for orphans:

```bash
curl -s http://localhost:5000/api/products/images/orphans -H "Authorization: Bearer $ADMIN" | jq
```

**Expected**: no `variant-…` key of that product is reported. (The orphan report is specs/033's; before
it existed this was checked by listing the `catalog_images` volume.)
