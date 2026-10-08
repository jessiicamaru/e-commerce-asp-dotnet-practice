# Catalog

The catalogue is what a shopper browses and what checkout prices: products filed under categories, each sold in one or more **variants** (a kit, a colour), with photographs, text in Vietnamese and English, and a price list per currency. It is owned by the Catalog service (REST on 5057, gRPC on 6057, database `ecommerce_catalog_db` on 5433). Sellers and administrators write it, moderators decide what goes on sale, and everyone reads it. Two ideas carry most of the weight. The **variant is what is bought**: it holds the SKU, the price and its own stock, and the first variant of a product reuses the product's id. And **Catalog reports, but never decides with, facts it does not own**: stock availability is a read model fed by Inventory, and nothing sells against it.

## What people can do

| Role | Capabilities |
| :-- | :-- |
| Shopper (anyone) | Browse the listing with paging, a category filter grouped by department (a department lists everything under it, specs/158), search and sort, a price range in the currency being browsed in and "in stock only" (specs/109); open a product page with its variants, photographs, "from" price and in-stock flag; read it in `vi` or `en` and priced in `VND` or `USD`; fetch product and variant images. Opening a product page reports one view. |
| Seller | List a product (it waits for review); add variants; reprice or deactivate a variant; set or remove a price per currency; translate the product and its options; upload or remove the product's and each variant's photograph; delete their own product; see their own listings in any review state; resubmit a rejected product. Every write is limited to their own products. |
| Moderator | Read the review queue (pending oldest first) or the history of approved and rejected products; approve, reject with a reason, or take down an approved product with a reason. |
| Administrator | Everything a moderator can do. Also: create, rename, translate, move and delete categories - from `/admin/categories` since specs/097 (#195), in a tree of departments and their categories since specs/158 (#363), where the slug is suggested from the name and never changes afterwards, searched by either name or the slug and twelve to a page (specs/133, in the browser - the list comes whole); list products that belong to the shop itself (they go on sale at once); write to any seller's product; find and reclaim orphaned image files; read the most-viewed products. |
| System | Record stock availability from Inventory's announcements; keep a read model of shop names from Identity; recompute each product's "from" price and availability from its variants. |

## How it works

**Products and variants.** `POST /api/products` creates a product and its first variant in one transaction. The first variant's id is the product's id. The command carries the default currency's price, the SKU and optional options (`Kit: Body only`). More variants come through `POST /api/products/{id}/variants` and get fresh ids. **The original name, description and category are corrected** through `PUT /api/products/{id}` (specs/124, #240) - the product's own columns, which no translation writes; a seller's change to an approved product, the category included, sends it back to review. The lookup carries `original` and `translations` (each language's own text, as stored, no fallback) so the seller's page edits what is stored rather than what a reader would see. Each variant has its own SKU, which is unique across the catalogue. Two variants of one product may not carry identical options. The product's `Price` column is not entered; `RecomputeProductRollupAsync` derives it as the cheapest active variant. Creating a product or a variant publishes `ProductCreatedEvent` or `ProductVariantCreatedEvent` through the outbox, and Inventory creates a stock row at zero for it. A new listing therefore has no stock until its seller sets some (`PUT /api/stock/{variantId}` on Inventory).

**Who owns a product.** `products.SellerId` is taken from the token: a seller's product is theirs, and an administrator's product has no seller and belongs to the shop itself. Every write handler calls `SellerOwnership.RequireCanWrite`. It lets an administrator through, lets a seller through for their own product, and otherwise throws the same `NotFoundException` a missing product throws. Shop names come from Catalog's own `sellers` table, which `SellerRegisteredConsumer` and `SellerRenamedConsumer` fill. A page of products therefore needs no call to Identity.

**Review before sale.** A seller's new product starts `Pending` (`ProductReview.StartsPending`). A product listed by an administrator starts `Approved`. Only `Approved` products are on the shelf (`Product.IsListed`). The public listing filters on it. The public lookup returns 404 to anyone but the seller and staff (`ProductReview.MaySee`). `ProductVariant.Sellable` requires it too, so both gRPC pricing paths report the variant unsellable and checkout refuses it. Staff move a product through `ProductReviewHandlers`. Each move is a guarded `UPDATE ... WHERE "ReviewStatus" IN (...)` inside `IProductRepository.TryReviewAsync`. The audit entry and the seller's notification are staged in the same transaction.

```mermaid
flowchart LR
    New["Seller lists a product"] --> P["Pending"]
    Admin["Administrator lists a product"] --> A["Approved - on sale"]
    P -- "approve (staff)" --> A
    P -- "reject with reason (staff)" --> R["Rejected"]
    A -- "take down with reason (staff)" --> R
    R -- "resubmit (seller)" --> P
    A -- "seller edits name, description or a photograph" --> P
```

**Language and currency.** Every request resolves a language (`?lang=`, then `Accept-Language`, then `Localization:DefaultLanguage` = `vi`) and a currency (`?currency=`, then `X-Currency`, then `Money:DefaultCurrency` = `VND`). Handlers read them through `IRequestLanguage` and `IRequestCurrency`. Responses carry `Content-Language`, `X-Currency` and `Vary: Accept-Language, X-Currency`. The product's own `Name` and `Description` columns hold the default-language text. `product_translations` and `variant_option_translations` hold the other languages. `Localized` picks per field, falling back to the default. Prices come from `Priced.Of`. It returns the variant's `variant_prices` row for the asked currency. For the default currency with no row, it returns `product_variants.Price`. Otherwise it returns `null`. There is no conversion and no fallback between currencies.

**Stock availability.** Inventory owns stock. Every stock change in Inventory publishes `StockAvailabilityChangedEvent` with `IsAvailable` and the time it was observed. Catalog records the flag per variant, guarded by `AvailabilityObservedAt` alone - an announcement wins only if it was observed later than the last one recorded, whatever its value, so a repeat still moves the clock and an older contrary one overtaken in flight loses (specs/054, #124). It then recomputes the product's flag as "any active variant available". Responses say `"InStock"` or `"OutOfStock"`, never a count. A real number comes from Inventory's public `GET /api/stock/{variantId}`.

```mermaid
sequenceDiagram
    participant S as Seller or Admin
    participant C as Catalog
    participant I as Inventory
    participant B as Shopper
    S->>C: POST /api/products
    C->>C: product + first variant (same id), outbox: ProductCreatedEvent
    C-->>I: ProductCreatedEvent (RabbitMQ)
    I->>I: stock row, QuantityOnHand = 0
    I-->>C: StockAvailabilityChangedEvent (IsAvailable = false)
    S->>I: PUT /api/stock/{variantId}
    I->>C: gRPC GetVariantOwners (sellers only)
    I-->>C: StockAvailabilityChangedEvent (IsAvailable = true, ObservedAt)
    C->>C: guarded update per variant, recompute product rollup
    B->>C: GET /api/products?searchTerm=may anh
    C-->>B: approved products, localized, priced in X-Currency, "InStock"
```

**Photographs.** Bytes live behind `IProductImageStore`. There are two implementations:
- `S3ProductImageStore` (specs/079) writes to an S3-compatible bucket that every Catalog instance shares. It is
  what the containers use, with SeaweedFS in development.
- `FileSystemProductImageStore` writes to a directory (`ProductImages:Root`). It is `dotnet run`'s default and
  assumes one instance.

`ProductImages:Store` chooses between them. There is no file-name column: the key is derived from the row as `{productId:N}-{ImageUpdatedAt ticks}.{ext}`, or `variant-{variantId:N}-{ticks}.{ext}` for a variant. An upload writes the new file first. It then switches the row with a guarded `UPDATE ... WHERE "ImageUpdatedAt" = @seen`, and only after that deletes the old file. The image address is `/api/products/{id}/image?v={ticks}`, which is cacheable for good when `v` matches. A variant with no photograph of its own reports the product's address in `VariantResponse.ImageUrl`. Deleting a product deletes its images after the row. Administrators can list and reclaim files that no row names (`/api/products/images/orphans`).

**Search and sort.** `GetPaginatedAsync` finds the ids whose `f_unaccent(lower(Name))` or `lower(Sku)` is `LIKE` the escaped term, UNIONed with the ids whose translation into the requested language matches the same way, and reads the page of those (specs/074). Sorting is by name (the default-language `Name`), or by price in the **requested** currency. Products with no price in that currency sort last.

**Views.** The storefront's product page sends `POST /api/products/{id}/view` once per product opened. Catalog counts it in `product_views` only for a listed product viewed by someone who is neither staff nor the product's seller. The endpoint answers 204 either way. See [admin insights](admin-insights.md).

## Rules and guarantees

1. **The variant is the sellable unit, and the first variant reuses the product's id.** Why: Inventory's stock rows, Cart's lines and Order's lines in three other databases held a product id before variants. Reusing the id made all of them correct without a cross-service backfill. Later variants get fresh ids, so nothing may assume either way. Inventory's `ProductId` columns hold a variant id. ([specs/020 research D2](../../specs/020-product-variants/research.md))
2. **A variant's SKU and options never change, and a variant is deactivated, never deleted.** `PUT .../variants/{variantId}` only reprices or toggles `IsActive`. Why: an order froze the SKU and option summary, and it has to keep describing what was bought. Two variants with the same set of options are refused with 409 in the handler, because the database cannot express "the same set" (specs/020 D5).
3. **`products.Price` and `products.Sku` are derived, not entered.** `Price` is the cheapest active variant in the default currency, and `Sku` is the first variant's. Why: an earlier Catalog image reads both columns on every query, so dropping them would strand a rollback (specs/020 D3).
4. **Catalog never stores or reports a stock count.** `Availability` defaults to `false` and nothing may sell, reserve or charge against it. Checkout reserves against Inventory's row under `FOR UPDATE`. Why: issue #4 was a `StockQuantity` set at creation, never written again and shown to shoppers. Showing "out of stock" by mistake costs a sale and corrects itself. Showing "in stock" by mistake takes an order that cannot be filled (specs/004 D4).
5. **An older availability announcement never overwrites a newer one.** The record is guarded by the observation time Inventory carried on the event, not by the arrival time. Why: redelivery and reordering are normal broker behaviour. A plain "write when the value differs" survives a duplicate but lets an overtaken "out of stock" win (specs/004 D3). An announcement for a variant Catalog does not hold is logged and discarded rather than retried.
6. **Somebody else's product is 404, never 403.** It is the same message a missing product gives. Why: a 403 confirms the id exists and belongs to somebody, which turns write endpoints into an enumeration tool. An administrator passes every ownership check, because moderation is the job. A product of the shop itself (`SellerId` null) cannot be adopted by a seller. The seller comes from the token, never from the body (specs/027).
7. **Opening a write to sellers means the controller attribute too.** Why: when these endpoints were `[Authorize(Roles = "Admin")]`, a seller was refused at the door, so the ownership code never ran. Every unit test still passed because tests send commands straight to handlers (comment in `ProductsController`).
8. **Nothing a seller lists is on sale until a moderator approves it, and the filter sits at the source.** Listing, search, lookup and both pricing paths all ask `Product.OnShelf` - approved **and** not withdrawn (`IsActive`), one definition since specs/092 (#185); before it, reads asked approval alone and writes asked both. Why: a client-side filter would miss checkout (specs/045 D2). The status is text. Its database default was `'Approved'`, so every product from before the feature stayed on sale; **since specs/093 (#184) it is `'Pending'`**, because after that backfill the default decides only an INSERT that omits the column - an image from before specs/045 running after a rollback - and `'Approved'` put that image's seller products on sale unreviewed. Such an image's own-shop products now wait for a moderator too: the safer mistake. ⚠️ The default is SQL in the migration, never `HasDefaultValue` in the model: the enum's CLR default is `Approved`, and EF would leave it out of every INSERT.
9. **A review decision happens once.** Approve and reject require `Pending`, take-down requires `Approved`, and resubmit requires `Rejected`. The guarded `UPDATE` decides, and the loser gets 409 and writes nothing. The audit entry (category `Moderation`) and the seller's notice (`ProductApproved`, `ProductRejected`, `ProductTakenDown`) commit in the same transaction. Rejecting and taking down need a reason of at most 500 characters, which the seller reads.
10. **After a rejection, the seller resubmits explicitly. After a seller edits an approved product, it goes back to review automatically.** `ProductReview.AfterSellerEditAsync` runs before the one save in eight handlers: set or remove a product translation, upload or remove the product image, upload or remove a variant image, and - since specs/056 (#126) - translate a variant option and add a variant, whose option words a shopper reads on the product page and an order line keeps. Why: an edit to an approved product is a change no moderator has seen. A rejected product is still being worked on, so its seller says when it is ready (specs/045 D3). What was decided with the user is "what a shopper sees goes back to review; prices and stock do not"; nothing staff edit sends a product back.
11. **A missing translation falls back per field. A missing price does not fall back at all.** A product with a Vietnamese name and no Vietnamese description shows both what exists. A variant with no price in the requested currency comes back with `price: null`, and checkout refuses it with 409 naming the currency. Why: the worst case of a text fallback is a shopper reading English. The worst case of a price fallback is a 40,000,000 VND camera sold for 1,600 VND, or charged at 40,000,000 USD ([specs/022 research D3](../../specs/022-multi-currency-prices/research.md)).
12. **Language and currency are chosen separately.** Why: most of this shop's customers read English and pay in dong. Deriving currency from language, from the delivery address or from IP geolocation was rejected (specs/022 D1). Responses vary on both headers because a shared cache would otherwise serve one shopper's Vietnamese or dong to the next.
13. **A price must fit its currency.** `Currency.Fits` refuses 9.99 in `VND` (0 decimals) in `CreateProduct`, `AddProductVariant`, `UpdateProductVariant` and `SetVariantPrice`. Zero is refused too. Why: an order once came back with a subtotal of 29.97 VND because a price had been entered as 9.99. Rounding what is computed does not fix a stored price (specs/022 D8). Rows written before the rule keep their amounts. The default currency's price lives on the variant and cannot be removed through the price endpoint.
14. **A translation in a language the shop does not speak is refused on write (400), but falls back to the default on read.** Why: a row nobody reads is a false claim about what the shop offers. A request in an unknown language is still a request worth answering.
15. **Search ignores diacritics on both sides, and is indexed** (specs/074, #113). `unaccent()` is only STABLE, so PostgreSQL would not index it; `f_unaccent` names its dictionary, which makes declaring it IMMUTABLE true, and `pg_trgm` GIN indexes over `f_unaccent(lower("Name"))` (products, translations) and `lower("Sku")` serve a `LIKE '%term%'`. The term's own `%`, `_` and `\` are escaped - Npgsql writes `ESCAPE ''` unless an escape is named. The translations are a UNION of ids rather than an `OR EXISTS`, which kept every product scanned even with the indexes. Measured on 100,000 products: a narrow search 452 ms to 1.2 ms, a broad one (10,000 matches) 426 ms to 81 ms.
15a. **A price range is of the "from" price the card shows, in the currency asked for, and indexed** (specs/109, #216).
    In the default currency it is `products.Price` over `IX_products_on_shelf_Price` - partial, with the listing's own
    shelf predicate, because a plain index lost to the review index in the plan. In any other currency it is the ids of
    a grouped join over that currency's active variant prices (`IX_variant_prices_Currency_Amount`) - ⚠️ never the
    price sort's correlated `MIN` per product, which would be a SubPlan run for every product, the shape rule 15 took
    out of the search. A product with no price in the currency is excluded, never converted (specs/022). `inStock`
    reads the availability read model, for display. A reversed or negative range is 400. `CatalogueFilterTests` reads
    both plans.
16. **An image's type is decided by its bytes.** JPEG, PNG or WebP by signature only. The client's `Content-Type` and the file name are ignored, and SVG is refused. Why: a header is a claim, and an HTML or SVG file served as an image from the shop's own origin is stored XSS. Uploads are limited to 2 MB, once by `[RequestSizeLimit]` before buffering and again while reading. Serving sets `X-Content-Type-Options: nosniff` ([specs/019 research D4-D6](../../specs/019-product-images/research.md)).
17. **The row always names a file that exists: write, then switch, then delete.** A concurrent replacement that loses the guarded switch deletes its own new file and answers 409. Why: delete-then-write leaves a product pointing at nothing if it fails half-way. The worst case of this order is an orphan file, which is waste rather than breakage (specs/019 D3). CHECK constraints on `products` and `product_variants` refuse half an image (a type without a time, or the reverse) and any type other than the three.
18. **A variant's photograph belongs to the variant, not to an option value. The fallback to the product's photograph is resolved on the server.** Why: `FUJI-XT5` and `FUJI-XT5-1855` are both black and look different. A client-side fallback is a second place to get it wrong, and getting it wrong shows the previous variant's picture, which looks like the feature working. The listing card deliberately keeps the product's picture ([specs/032 research](../../specs/032-variant-images/research.md)).
19. **Variant image keys carry a `variant-` prefix.** Why: the first variant shares the product's id, so without the prefix the two keys differ only by two timestamps. Setting both in the same tick would make one image overwrite the other. `FileSystemProductImageStore` accepts only keys matching `^(variant-)?[a-z0-9]+-[0-9]+\.(jpg|png|webp)$` before it builds a path.
20. **Deleting a product deletes its images after the row, outside the transaction, and never fails because of them.** Why: before specs/029 the row went and the bytes stayed forever, and two orphans were found only by listing the directory. Deleting bytes first would leave a live row naming a missing file. A leftover PNG must not stop a product from being removed ([specs/029 research D2-D3](../../specs/029-delete-product-image/research.md)). The deletion publishes `ProductDeletedEvent` with every variant id, so Inventory drops the stock rows - and, in the same transaction, releases their **held** reservations ("Product deleted"); settled ones stay as the orders' history (specs/090, #181). Nothing cascades: there is no foreign key from `stock_reservations`.
21. **Orphan reclamation reads the live keys first, and a failure there is fatal.** If that read failed and the scan went on with an empty set, every file would become a candidate and the reclaim would delete the whole catalogue's images. The response publishes `liveKeys` so a nonsensical answer is visible. Files younger than `ProductImages:OrphanGraceHours` (default 24) are never orphans. The reclaim takes no key list: it reconciles again and removes what it finds. Nothing runs it on a timer, because it destroys bytes nobody can recreate ([specs/033 research](../../specs/033-image-reconciliation/research.md)).
22. **A product view is its own request, counted by the database.** One `INSERT ... ON CONFLICT ("ProductId", "Day") DO UPDATE SET "Views" = "Views" + 1`. Why: `GET /products/{id}` is called repeatedly by the seller page (once per currency) and by focus refetches, so counting reads would count both. A read-then-write increment loses views that arrive together ([specs/047 plan D2-D3](../../specs/047-admin-insights/plan.md)).
23. **An anonymous read of the listing, a product, the categories or a shop page is answered from memory, and emptied after every committed catalogue write** (specs/157, #361). ASP.NET Core output caching keeps the whole answer - body, `Content-Language`, `X-Currency`, `Vary` - for 30 s (`Caching:CatalogueSeconds`, 0 off), apart by query and by the *negotiated* language and currency; only 200s, and never a request carrying `Authorization`. `CatalogueWrites`, an EF Core interceptor, evicts after the commit of any statement writing `products`, `product_variants`, `variant_prices`, the translations, `categories` or `sellers` - EF, guarded SQL or a consumer alike - and never on a rollback, a view or a saved product. A read that overlapped an eviction keeps nothing: each eviction advances a generation first, and an answer is stored only if none passed since its request began (specs/159, #366 - the browser flows saw a product stay "out of stock" for 30 s after a seller stocked it). Why: some forty handlers, a dozen consumers and many guarded statements write those tables, and an eviction call in each would be missed by the next one written. What decides - pricing over gRPC, stock, ownership - is never cached.
24. **Categories are departments and their categories, two levels** (specs/158, #363). `CategoryTree` checks a parent on create and on `PUT /api/categories/{id}/parent`: it exists, is not the category, and has no parent of its own; a category with categories under it stays a department. Filtering by a department lists its categories' products too - one subquery on the parent's index, no recursion. Deleting a department with categories under it is a worded 409. Why two levels: every screen that shows the tree is a list with one indent, and the six verticals of the seed are exactly departments and categories ([research D1](../../specs/158-category-tree/research.md)). Why a separate move endpoint: adding the parent to the rename would make every existing caller send null and lift categories out of their departments.
25. **Products have specifications, declared per category** (specs/159, #366). A category declares what its products are compared by - a **text** (a model number, a measurement, an author; shown as written, never translated) or a **choice** of options, each translated - and a product has its category's and its department's, department first. `PUT /api/products/{id}/specifications` replaces the product's whole set: each value must belong to a specification that applies, a choice must be one of its options, a text 1-200 characters; a seller's change to an approved product sends it back to review (specs/045), and it is audited. The lookup carries them as a table in the reader's language, and the listing keeps products holding every `optionIds` given - one `EXISTS` per option on `product_specifications(OptionId)`. Deleting a specification or an option a product uses is a worded 409; deleting a product takes its values (cascade). Why only choices are filterable and translated: words that repeat across products are exactly what a choice is, and equal values are only equal when they are options ([research D2](../../specs/159-product-specifications/research.md)).

## Data

All in `ecommerce_catalog_db`. See the [data model](../reference/data-model.md#catalog---ecommerce_catalog_db-14-tables).

| Table | What it holds |
| :-- | :-- |
| [`products`](../reference/data-model.md#products) | Name and description (default language), category, `SellerId`, derived `Price` and `Availability`, image columns, `ReviewStatus` / `ReviewReason` / `SubmittedAt` / `ReviewedAt` / `ReviewedBy`, `RatingAverage` / `RatingCount`. |
| [`product_variants`](../reference/data-model.md#product_variants) | One sellable shape: unique `Sku`, default-currency `Price`, `OptionSummary`, `IsActive`, per-variant `Availability`, its own image columns. |
| [`variant_options`](../reference/data-model.md#variant_options) | Name and value pairs of a variant, unique on (`VariantId`, `Name`). |
| [`variant_prices`](../reference/data-model.md#variant_prices) | A variant's price in a non-default currency, unique on (`VariantId`, `Currency`). |
| [`product_translations`](../reference/data-model.md#product_translations) | Name and description per language. |
| [`variant_option_translations`](../reference/data-model.md#variant_option_translations) | Option name and value per language. |
| [`categories`](../reference/data-model.md#categories) | Name, slug (fixed once created), description, `ParentCategoryId` - null for a department, else the department it sits under (two levels, specs/158; `RESTRICT` foreign key). `IsActive` is dead data - every row `false`, nothing reads it (specs/097 D3). |
| [`category_translations`](../reference/data-model.md#category_translations) | Category name and description per language. |
| [`category_specifications`](../reference/data-model.md#category_specifications) | What a category's products are compared by: code (fixed, unique in the category), default-language name, `Kind` (`Text`/`Choice`), position (specs/159). Translations in `category_specification_translations`. |
| [`specification_options`](../reference/data-model.md#specification_options) | A choice's options: code (unique in the specification), default-language value, position; translations in `specification_option_translations`. |
| [`product_specifications`](../reference/data-model.md#product_specifications) | A product's value per specification: an option or a text (a CHECK says exactly one); cascade with the product, restrict on the specification and option. |
| [`sellers`](../reference/data-model.md#sellers) | Read model of shop names, descriptions (specs/099) and suspension, fed by Identity's events. |
| [`product_views`](../reference/data-model.md#product_views) | Views per product per shop day (UTC before specs/082). |
| [`product_reviews`](../reference/data-model.md#product_reviews), [`review_eligibility`](../reference/data-model.md#review_eligibility) | See [ratings and reviews](ratings-and-reviews.md). |

**Off the shelf, nothing hangs on it for the public either** (specs/081, #166).
- A product that is not on the shelf (`Product.OnShelf`: approved and not withdrawn, specs/092) is the public
  lookup's 404 and absent from the listing, and so are its reviews and questions, except to its seller and staff
  (`ProductReview.MaySee`).
- Its images are served only to an address carrying the image's own **`ImageAccessKey`** (`&k=`). This is an
  unguessable Guid, new with every image and written by the guarded statement that switches it. It is handed out
  only in the responses its reader may see. The address needs a key because a browser's image request carries no
  token.
- On sale, an image is served with or without the key, so cached addresses keep working. Off the shelf, the
  response is `private, no-cache`.

Image bytes are not in the database. They are objects in the `product-images` bucket (specs/079), or files in the
store's directory under `dotnet run`, named by the key derived from the row.

**Object storage** (specs/079, #114):
- A key is stored **only when new**, with `If-None-Match: *`, as the directory's move is.
- A read copies the object into memory; images are at most 2 MB.
- The listing pages through `ListObjectsV2` and never holds the bucket.
- Startup creates the bucket and writes a probe, or **refuses to start** and says why.
- The orphan report reads the same bucket from any instance, and its `note` says the store is shared.
- The images from the old `catalog_images` volume are copied in at startup (`ProductImages:ImportFrom`, the volume
  mounted read-only). The copy is idempotent by key and safe on several instances at once, and it never deletes the
  directory.

| Setting | Meaning |
| :-- | :-- |
| `ProductImages:Store` | `FileSystem` (the default) or `S3`. |
| `ProductImages:S3:ServiceUrl`, `Bucket`, `AccessKey`, `SecretKey` | Where the bucket is and who Catalog is to it. A missing one refuses to start, naming it. |
| `ProductImages:S3:Region`, `PageSize` | The signing region (default `us-east-1`) and how many keys one listing page asks for (1 to 1000; outside that, Catalog refuses to start). |
| `ProductImages:ImportFrom` | A directory whose images the bucket lacks are copied in at startup. |

## API

Through the gateway (`/api/products/**`, `/api/categories/**`). Full list: [API reference](../reference/api.md#catalog-38).

| Method | Path | Who |
| :-- | :-- | :-- |
| `GET` | `/api/products` | anyone (on the shelf only; `pageNumber`, `pageSize`, `categoryId` - a department's includes its categories' (specs/158), `optionIds` - every specification option given (specs/159), `searchTerm`, `sortBy` = `name_desc` / `price_asc` / `price_desc`, `sellerId` - one shop's, specs/099; `minPrice`, `maxPrice`, `inStock` - specs/109) |
| `GET` | `/api/shops/{sellerId}` | anyone - a shop's name, description and count on the shelf; 404 unknown, unnamed or suspended (specs/099, see [marketplace](marketplace.md)) |
| `GET` | `/api/products/{id}` | anyone (404 unless approved, or the caller is its seller or staff) |
| `GET` | `/api/products/mine` | Seller |
| `POST` | `/api/products` | Seller, Admin |
| `DELETE` | `/api/products/{id}` | Seller, Admin |
| `PUT` | `/api/products/{id}` (`name`, `description`, `categoryId`) | Seller (own), Admin - specs/124 |
| `POST` | `/api/products/{id}/variants` | Seller, Admin |
| `PUT` | `/api/products/{id}/variants/{variantId}` | Seller, Admin |
| `PUT` / `DELETE` | `/api/products/{id}/variants/{variantId}/prices/{currency}` | Seller, Admin |
| `PUT` / `DELETE` | `/api/products/{id}/translations/{language}` | Seller, Admin |
| `PUT` | `/api/products/{id}/options/{optionId}/translations/{language}` | Seller, Admin |
| `GET` | `/api/products/{id}/image` | anyone |
| `PUT` / `DELETE` | `/api/products/{id}/image` | Seller, Admin |
| `GET` | `/api/products/{id}/variants/{variantId}/image` | anyone |
| `PUT` / `DELETE` | `/api/products/{id}/variants/{variantId}/image` | Seller, Admin |
| `GET` / `DELETE` | `/api/products/images/orphans` | Admin |
| `GET` | `/api/products/review` | Admin, Moderator (`status` = `Pending` / `Approved` / `Rejected`) |
| `POST` | `/api/products/{id}/approve` | Admin, Moderator |
| `POST` | `/api/products/{id}/reject` | Admin, Moderator (body `{ reason }`) |
| `POST` | `/api/products/{id}/take-down` | Admin, Moderator (body `{ reason }`) |
| `POST` | `/api/products/{id}/resubmit` | Seller, Admin |
| `POST` | `/api/products/{id}/view` | anyone (always 204) |
| `GET` | `/api/categories` | anyone |
| `POST` | `/api/categories` | Admin (409 when the slug is taken - it was a 500 until specs/097; 400 when `parentCategoryId` is missing or not a department, specs/158) |
| `PUT` | `/api/categories/{id}` | Admin - the default-language name and description; the slug never changes (specs/097) |
| `PUT` | `/api/categories/{id}/parent` (`parentCategoryId`, null for the top) | Admin - moves it; audited `CategoryMoved`; 400 when the move would make a third level (specs/158) |
| `GET` | `/api/categories/{id}/specifications` | anyone - what applies to products in that category, department's first (specs/159) |
| `POST` / `PUT` / `DELETE` | `/api/categories/{id}/specifications[/{specId}[/translations/{lang}]]` and `.../{specId}/options[/{optionId}[/translations/{lang}]]` | Admin - declare, rename, translate, remove (409 while a product uses it) |
| `PUT` | `/api/products/{id}/specifications` (`values`) | Seller (own), Admin - the whole set; a seller's approved product back to review (specs/159) |
| `DELETE` | `/api/categories/{id}` | Admin (409 while products are filed under it, or categories sit under it - specs/158) |
| `PUT` / `DELETE` | `/api/categories/{id}/translations/{language}` | Admin |
| `GET` | `/api/stock/{productId}` (Inventory) | anyone - the real count, keyed by variant id |

Catalog also serves `CatalogPricing` (Cart and Order price variants at checkout) and `CatalogOwnership` (Inventory asks who owns a variant) over gRPC. See [gRPC reference](../reference/grpc.md).

## Messages

See [messages reference](../reference/messages.md).

| Message | Direction | Why |
| :-- | :-- | :-- |
| `ProductCreatedEvent` | published | Inventory creates the first variant's stock row at zero. |
| `ProductVariantCreatedEvent` | published | Inventory creates the new variant's stock row. |
| `ProductDeletedEvent` | published | Carries every variant id, so Inventory drops their stock rows and releases their held reservations (specs/090). |
| `StockAvailabilityChangedEvent` | consumed (`StockAvailabilityChangedConsumer`) | Records per-variant availability. From an Inventory that sends no `VariantId`, the product id is used. |
| `SellerRegisteredEvent`, `SellerRenamedEvent` | consumed (`SellerRegisteredConsumer`, `SellerRenamedConsumer`) | Keeps `sellers.ShopName`. A rename writes no product. |
| `SellerDescribedEvent` | consumed (`SellerDescribedConsumer`) | Keeps `sellers.Description` behind `DescriptionObservedAt`, for the shop's page (specs/099). |
| `AuditEntryRecorded`, `UserNotificationRequested` | published | Through `IAuditTrail` and `INotifier`: listing, editing, deleting and every review decision. |

## Storefront

| Path | What it does |
| :-- | :-- |
| `client/apps/storefront/src/pages/catalog/` | The listing. Search, category and sort live in the URL so a result can be shared. Pages of `PAGE_SIZE` = 12. |
| `client/apps/storefront/src/components/catalog/catalog-filters/`, `catalog-hero/` | Search box, category combobox and sort select; the landing banner. |
| `client/apps/storefront/src/components/product/product-card/` | A listing card: the product's photograph, "from" price, availability and star average. |
| `client/apps/storefront/src/pages/product/` | The product page. Variant chooser, price, add to cart, reviews, and one `recordView` per product opened (guarded by a ref). |
| `client/apps/storefront/src/components/product/variant-chooser/`, `product-image/`, `availability/`, `stock-badge/` | Choosing a shape; the photograph or a tinted placeholder; the in-stock flag; Inventory's real count. |
| `client/packages/core/src/components/shared/price/`, `components/layout/currency-switcher/`, `language-switcher/` | Price formatting with `Intl.NumberFormat(language, { currency })`; choosing currency and language independently (both stored in `localStorage`, sent as headers). |
| `client/apps/storefront/src/pages/shop-products/`, `shop-product-new/`, `shop-product/` | A seller's listings, the create form (price labelled in the default currency), and the product editor (prices read once per currency, stock, photographs, withdraw). |
| `client/apps/storefront/src/components/seller/variant-editor/`, `components/shared/image-dropzone/` | One variant's prices, stock and photograph; drag-and-drop upload that refuses SVG and files over 2 MB before sending. |
| `client/apps/storefront/src/components/product/review-badge/`, `components/seller/review-banner/` | Where a listing stands with the moderators; the reason for a rejection and "send back for review". |
| `client/apps/back-office/src/pages/admin-products/`, `admin-moderation/` | The review queue with a tab per status and a reason dialog; a moderator's dashboard (waiting counts and their own decisions from `GET /api/audit/mine`). |
| `client/packages/core/src/services/product/`, `hooks/product/`, `services/moderation/`, `hooks/moderation/`, `services/category/` | The axios classes and TanStack Query hooks behind the pages. |

## Tests

Server tests run against a real PostgreSQL (`Ecommerce.Catalog.Tests`, port 5433).

| Class | What it proves |
| :-- | :-- |
| `CatalogueCacheTests` | Through Catalog's own registration and middleware order: a repeated anonymous read never reaches the handler and replays its headers; another negotiated language, currency or query is another answer (`en-GB` and `en` one); a request with `Authorization` is neither served nor stored; a 404 is not kept; eviction empties it; 0 switches it off; a bad setting stops the start. A read that saw an eviction while it was being made is not kept (fails with the generation check removed). |
| `CatalogueWritesTests` | Against PostgreSQL: an EF save evicts; a guarded statement in a transaction evicts after the commit and not before; a rollback evicts nothing; a statement outside a transaction is its own commit; a view, a saved product or `FOR UPDATE` evicts nothing; which statement texts count. Evicting before the commit fails two of them. |
| `CategoryTreeTests` | Created under a department and listed with it; a missing parent, a third level, itself or a department with categories under it moved under another - each refused on `ParentCategoryId`; moves audited as `CategoryMoved` and a move to where it is records nothing; a rename moves nothing; a department with a category under it is a 409 until it is empty; a department lists its categories' products and a category only its own. Narrowing the filter back to the category alone fails the last. |
| `ProductSpecificationTests` | A category has its department's specifications then its own, in the reader's language; declaring is checked (kind, options, code, a taken code 409); a product's values read back as a table, options translated and texts as written; values that do not fit are refused naming where; the set is replaced whole; a seller's change sends an approved product to review and an unchanged one does not, another seller's is 404; what a product uses is not deleted under it and goes with the product; the listing keeps products holding every option. Dropping the review call fails the seller test. |
| `VariantTests` | Creating a product creates its first variant with the product's id; the "from" price follows the cheapest active variant; duplicate SKUs and identical option sets are refused; availability is per variant; the specs/020 migration gave every existing product one variant with its own id; options are summarised in a stable order. |
| `AvailabilityTests` | An announcement makes a product available; an unannounced product reads as unavailable; ten deliveries record once; an older observation arriving later does not win; an unknown product's announcement is discarded; creating a product accepts no stock quantity. |
| `SellerOwnershipTests` | Every write to another seller's product is 404 (delete, add variant, reprice, translate, option translate, price, photograph); the shop's own product cannot be adopted; an administrator may touch any listing; "my listings" holds only mine; a shop rename changes listings without writing a product; an overtaken rename does not win. |
| `ProductReviewTests` | A seller's new product is hidden and unsellable until approved; the shop's own is on sale at once; approval happens once and notifies the seller; rejection carries its reason and can be resubmitted; take-down removes it from the shelf; editing the name sends an approved product back and a price change does not; the queue is oldest submission first. |
| `TranslationTests`, `CategoryTranslationTests`, `LanguageNegotiationTests` | Translated reads say which language they are in; the fallback is per field; writes are upserts; unsupported languages are refused on write; option text is translated; search ignores diacritics and looks at both the translation and the original; `?lang=` beats `Accept-Language`; `Content-Language` and `Vary` are set. |
| `VariantPriceTests`, `RequestCurrencyTests` | A variant with no price in a currency is not sold in it; the "from" price and "price varies" use only variants priced in the asked currency; zero and amounts the currency cannot hold are refused; the default currency's price cannot be removed; currency is not taken from language; dong has no decimals. |
| `ProductImageTests` | Upload, replace, remove; the bytes decide the type; oversize, empty or lying files are refused and the old image stays; a failed write or delete leaves the row correct; a lost race is 409 with no file left behind; CHECK constraints; product deletion deletes every image; variant photographs fall back to the product's; a product and its first variant never share a file. |
| `S3ProductImageStoreTests` (12, against SeaweedFS) | What is saved reads back and is gone once deleted. A key is stored once, and a second write is refused. Keys that are not image keys are refused before any request. The listing pages (2 at a time) and leaves out the probe. Two instances see each other's images. The orphan report from either instance finds only what no row names. The import copies what the bucket lacks and nothing twice, even run four times at once. A wrong secret or a missing setting refuses to start. Every one of seven mutations turns one of these red. |
| `OrphanImageTests` | Live product and variant images are never orphans; young files are skipped; a catalogue read that fails reports nothing rather than everything; one refusing key does not stop the rest; the store's own probe file is not waste. |
| `DeleteProductTests`, `DeleteCategoryTests` | A deleted product takes its variants, options, prices and translations, announces every variant, and frees its SKU; a category with products is refused with the count. |
| `ProductViewTests` | A shopper's view counts and twenty concurrent views count twenty; staff, the seller and unlisted products do not count; the most viewed come first. |
| `VariantOwnershipTests`, `VariantSellerPricingTests` | The gRPC ownership and pricing answers name the seller and shop, and say nothing rather than invent a name. |
| `AuditTests` | Listing, pricing and withdrawing are recorded once; a refused change is not recorded. |

Client tests (Vitest): `pages/product/index.test.tsx` (the chosen variant's photograph, the fallback, one view per product however often it renders), `pages/admin-products/index.test.tsx`, `pages/admin-moderation/index.test.tsx`, `components/seller/review-banner/index.test.tsx`, `pages/shop-product/index.test.tsx` (prices read per currency, stock by variant id, variant photographs), `pages/shop-product-new/index.test.tsx`, `pages/shop-products/index.test.tsx`, `components/shared/image-dropzone/index.test.tsx`, `services/product/index.test.ts`, `hooks/product/index.test.tsx`.

Bruno: `bruno/product/` (variants, translations, search without diacritics, dollar prices, an unpriced variant, images, orphan report, deletion), `bruno/category/`, `bruno/seller/` (a product goes pending, approved, renamed back to pending, taken down, resubmitted, with the 403s), and `bruno/security-checks/` (customers cannot write products, oversize and non-image uploads are 400, a seller cannot read the orphan report). `server/seed/seed-catalogue.py` seeds the catalogue - one file per vertical in `seed/catalogue/`, checked by `seed/catalogue.py` (CI) - through the gateway as an administrator, and `server/seed/clean-test-debris.py` removes products that no file there names. The client's `locales/shop-wording.test.ts` keeps camera words out of the shop's own sentences (specs/156).

## Known limits

- **The read cache is per instance** (specs/157). With several Catalog instances, a write evicts only its own
  instance's memory; another can answer from its copy until it expires (30 s). Within one instance, a read that began
  before a commit no longer stores its answer (the generation check, since specs/159); only the instant between an
  answer's headers and its body being stored is left uncovered.
- **Catalog still streams every image itself.** There is no CDN and no presigned URL straight to the bucket
  (specs/079, out of scope).
- **The directory store still assumes one instance.** It is `dotnet run`'s default. The containers use the bucket.
- **Sorting by name uses the default-language `Name`**, not the translated one. Search matches the SKU with a plain `LIKE` and does not search descriptions.
- **An image address somebody already holds keeps opening that image** after its product leaves the shelf,
  until the image is replaced (specs/081: the key is a capability, not a signed and expiring URL). Anybody who holds
  it saw the photograph already.
- **Pending products would show during a rollback** to an image from before specs/045, which ignores `ReviewStatus` (specs/045 D1).
- **Prices entered before specs/022's rule keep fractional dong amounts** until somebody reprices them.
- **Order lines freeze no image.** An order for a deleted product loses its picture (specs/029 D1, specs/032 D8).
- **Products left by older test runs stay** until somebody cleans them. Since specs/073 (#118) Bruno's `teardown` folder and the scripts' `trap ... EXIT` remove what each run makes. What earlier runs left is still there, and `seed/clean-test-debris.py` removes it.
- Saving a product for later is [saved products](saved-products.md) (specs/075), and asking its seller is [product questions](product-questions.md) (specs/076).

## History

| Spec | PR | What it added |
| :-- | :-- | :-- |
| [004-stock-single-source](../../specs/004-stock-single-source/) | [#5](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/5) | Removed `StockQuantity`; availability as a read model of Inventory's announcements, guarded by observation time. |
| [016-storefront-catalog](../../specs/016-storefront-catalog/) | [#46](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/46) | Storefront listing, search and product page. |
| [019-product-images](../../specs/019-product-images/) | [#54](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/54) | Product image store, write-switch-delete, type by bytes, versioned cacheable address. |
| [020-product-variants](../../specs/020-product-variants/) | [#57](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/57) | Variants as the sellable unit; the first variant reuses the product's id; per-variant availability; `PriceVariants`. |
| [021-internationalisation](../../specs/021-internationalisation/) | [#58](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/58) | Product and option translations, language negotiation, diacritic-insensitive search. |
| [022-multi-currency-prices](../../specs/022-multi-currency-prices/) | [#59](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/59) | `variant_prices`, `X-Currency`, null price instead of conversion, minor-unit validation. |
| - | [#60](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/60) | A catalogue of real cameras, seeded through the API. |
| [024-delete-product](../../specs/024-delete-product/) | [#61](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/61) | Deleting a product for good. |
| [025-storefront-redesign](../../specs/025-storefront-redesign/) | [#62](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/62) | Deleting an empty category, with the storefront's redesign. |
| specs/026 (no folder) | [#63](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/63) | Category translations. |
| [027-seller-accounts](../../specs/027-seller-accounts/) | [#64](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/64) | `products.SellerId`, `SellerOwnership`, the `sellers` read model. |
| [097-category-admin](../../specs/097-category-admin/) | [#204](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/204) | `/admin/categories`, `PUT /api/categories/{id}`, a duplicate slug is 409 (#195). |
| [028-seller-console](../../specs/028-seller-console/) | [#65](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/65) | Seller pages to list, price, photograph and withdraw products. |
| [029-delete-product-image](../../specs/029-delete-product-image/) | [#68](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/68) | Deleting a product deletes its image. |
| - | [#69](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/69) | Seeded photographs, kept out of the repository. |
| [032-variant-images](../../specs/032-variant-images/) | [#73](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/73) | Variant photographs with a server-resolved fallback and the `variant-` key prefix. |
| [033-image-reconciliation](../../specs/033-image-reconciliation/) | [#74](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/74) | Orphan image report and reclaim. |
| [045-product-review](../../specs/045-product-review/) | [#97](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/97) | Review before sale, the moderators' queue, edits that send a product back. |
| [046-product-reviews](../../specs/046-product-reviews/) | [#98](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/98) | `RatingAverage` and `RatingCount` on products. See [ratings and reviews](ratings-and-reviews.md). |
| [047-admin-insights](../../specs/047-admin-insights/) | [#99](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/99) | `product_views` and top viewed. See [admin insights](admin-insights.md). |
| [079-object-storage](../../specs/079-object-storage/) | [#163](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/163) | Images in an S3-compatible bucket shared by every instance (SeaweedFS in development), the old volume imported at startup (#114). |
| [109-catalogue-filters](../../specs/109-catalogue-filters/) | #229 | `minPrice`, `maxPrice`, `inStock` on the listing; `IX_products_on_shelf_Price`, `IX_variant_prices_Currency_Amount` (#216). |
| [124-seller-edits-listing](../../specs/124-seller-edits-listing/) | #263 | `PUT /api/products/{id}`; `original` and `translations` on the lookup; the seller's page edits details, each language's text and adds variants, signed in (#240). |
| [158-category-tree](../../specs/158-category-tree/) | #365 | Departments and their categories: `CategoryTree`, the move endpoint, a department's 409, the listing by department; the storefront's grouped filter, department chips and breadcrumb; the back office's tree; the seed's departments. |
| [159-product-specifications](../../specs/159-product-specifications/) | [#367](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/367) | Specifications declared per category and inherited from the department, filled in by the seller, shown as a table and filtered on; five new tables; the back office declares them; the seed fills every product. |
| [156-generic-catalogue](../../specs/156-generic-catalogue/) | #360 | A shop for anything: the storefront's own words name no kind of goods, the no-photograph tile is a parcel, and the seed is one file per vertical in `seed/catalogue/`, checked by `seed/catalogue.py` in CI. |
| [157-catalogue-cache](../../specs/157-catalogue-cache/) | #362 | Anonymous public reads answered from memory, varying by query, language and currency; emptied after every committed catalogue write by an EF Core interceptor. |
