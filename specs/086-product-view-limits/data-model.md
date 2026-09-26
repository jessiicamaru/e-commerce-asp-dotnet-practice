# Data Model: Honest view counts

> Written on 2026-09-27, after the feature merged (#178), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

One table added to `ecommerce_catalog_db`, one migration. `product_views` is unchanged in shape.

---

## `product_viewers` (new)

Who has already viewed a product on a shop day. Entity `ProductViewer`, configuration
`ProductViewerConfiguration`.

| Column | Type | Constraints | Meaning |
| :--- | :--- | :--- | :--- |
| `ProductId` | `uuid` | PK part; FK → `products.Id`, `ON DELETE CASCADE` | The product viewed |
| `Day` | `date` | PK part | The shop day of the view (specs/082) |
| `Viewer` | `character varying(64)` | PK part, not null | SHA-256, upper-case hex, of `user:<id>` or `visitor:<uuid>` |

**Primary key** `PK_product_viewers` on (`ProductId`, `Day`, `Viewer`): the claim. The one insert that succeeds for
a viewer on a day is the one view counted. No other index: the delete reads by the key's leading columns.

**Size**: at most one day of viewers per product that was viewed, because a product's first view of a day deletes
its earlier days in the same statement. A product not viewed today keeps yesterday's rows until its next view.

## `product_views` (unchanged)

| Column | Type | Constraints |
| :--- | :--- | :--- |
| `ProductId` | `uuid` | PK part; FK → `products`, cascade |
| `Day` | `date` | PK part; indexed |
| `Views` | `integer` | not null |

## How a view is written

| Viewer | Statement | Effect of a repeat the same day |
| :--- | :--- | :--- |
| none (no token, no body) | `INSERT INTO product_views ... ON CONFLICT DO UPDATE SET "Views" = "Views" + 1` (specs/047) | Counts again |
| hashed user or visitor | the CTE in [research.md D4](./research.md): delete earlier days, insert the viewer `ON CONFLICT DO NOTHING RETURNING 1`, increment only from that row | The insert returns nothing; `Views` is unchanged |

Not written at all, as before: a product that does not exist or is not listed, or a caller who is an administrator,
a moderator or the product's seller (specs/047).

## Migration `20260926174014_AddProductViewers`

`CreateTable("product_viewers")` with the three columns, the composite primary key and the cascading foreign key;
`Down` drops the table. Additive only: an earlier Catalog image ignores the table and counts every call again.

## Browser storage

`localStorage["visitor-id"]` - a UUID made by `crypto.randomUUID()`. Not personal data by itself; never sent
anywhere but the view endpoint.
