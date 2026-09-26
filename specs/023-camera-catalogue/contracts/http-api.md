# HTTP Contract: A Catalogue of Real Cameras

> Written on 2026-09-27, after the feature merged (#60), from the code at that merge, the pull request
> and docs/features/catalog.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md D5, D6](../research.md)

**One response changed, additively. No endpoint was added, and no message contract changed.** The rest
of this file records every call the seeder makes, all through the gateway (`GATEWAY_URL`, default
`http://localhost:5000`), every write as an administrator.

---

## The change: `VariantOptionResponse` gains `id`

Wherever a product's variants are returned (`GET /api/products/{id}`, and the create and add-variant
responses), each option now reads:

```json
{ "id": "0199...", "name": "Kit", "value": "Body only" }
```

where it used to read `{ "name", "value" }`. The `id` is what
`PUT /api/products/{id}/options/{optionId}/translations/{language}` takes - without it that endpoint
(specs/021) "cannot be called by anything outside the database".

`optionSummary` on a variant does not change shape; its **options are now in a fixed order** - by the
stored option name, case-insensitive - whether the request names a language or not. In Vietnamese:
`Bộ: Chỉ thân máy · Màu: Đen`; in English, the same order in English words: `Kit: Body only · Colour:
Black`.

## Every call the seeder makes

Each request carries `Accept-Language: vi` and `X-Currency: VND` - "seeded text is the DEFAULT language
and the DEFAULT currency".

| Step | Request | Auth | Notes |
| :--- | :--- | :--- | :--- |
| Sign in | `POST /api/auth/login` `{ email, password }` | anonymous | `ADMIN_EMAIL` / `ADMIN_PASSWORD`; reads `token` |
| Categories present | `GET /api/categories` | anonymous | matched by `slug` |
| Add a category | `POST /api/categories` `{ name, description, slug, parentCategoryId: null }` | Admin | |
| Products present | `GET /api/products?pageNumber=N&pageSize=100` | anonymous | until `hasNextPage` is false; SKU → id |
| Add a product | `POST /api/products` `{ name, description, price, sku, categoryId, options }` | Admin | Vietnamese text, dong price, the first variant's options |
| English text | `PUT /api/products/{id}/translations/en` `{ name, description }` | Admin | |
| Read variants | `GET /api/products/{id}` | anonymous | variant SKUs and ids; option ids |
| Add a variant | `POST /api/products/{id}/variants` `{ sku, price, options }` | Admin | not for the first variant |
| Dollar price | `PUT /api/products/{id}/variants/{variantId}/prices/USD` `{ amount }` | Admin | a price somebody decided, never a conversion |
| Stock | `PUT /api/stock/{variantId}` `{ quantityOnHand }` | Admin (Inventory) | a 404 is retried, up to 20 × 0.5 s |
| English option | `PUT /api/products/{id}/options/{optionId}/translations/en` `{ name, value }` | Admin | option matched **by name** |

Any other non-2xx stops the run with the method, path, status and the first 400 characters of the body.

## Why a stock write can 404 for a real variant

Catalog publishes `ProductCreatedEvent` / `ProductVariantCreatedEvent` through its outbox, and Inventory's
`ProductCreatedConsumer` / `ProductVariantCreatedConsumer` register the stock row a moment later. Until
then Inventory correctly answers 404. Those messages did not change; the seeder only waits for them.
