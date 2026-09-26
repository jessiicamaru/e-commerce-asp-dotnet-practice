# Contracts: A saved product back on sale by any route tells whoever saved it

**Feature**: [spec.md](../spec.md)

**No HTTP route, request, response, message shape or gRPC contract changes.** More handlers publish two existing
messages, and their default words change.

## `Ecommerce.Contracts.Activity.UserNotificationRequested` (unchanged shape)

| Field | Value |
| :-- | :-- |
| `RecipientId` | each saver |
| `Kind` | `SavedBackInStock` (declared in `notification-kinds.json`, required data `product`) |
| `Data` | `{ "product": <product name, default language> }` |
| `Link` | `/products/{productId}` |

Published by (new rows in **bold**):

| Handler | When |
| :-- | :-- |
| `RecordStockAvailabilityCommandHandler` | the rollup flips back in stock and the product is listed and active (unchanged) |
| **`UpdateProductVariantCommandHandler`** | same condition, after the variant edit |
| **`AddProductVariantCommandHandler`** | same condition, after the variant is added |
| **`SetVariantPriceCommandHandler`** | same condition, after the price is set |
| **`ProductReviewHandlers` (approve)** | `Pending` → `Approved` won, and the product is active and in stock |

## `Ecommerce.Contracts.Identity.EmailRequested` (unchanged shape)

Template `SavedBackInStock`, language `EmailTemplate.ReadersLanguage` (Identity fills it from `users.Language`), data
`{ "productId", "product" }`. Published alongside every notice above.

## Default words (changed)

| Where | Before | After |
| :-- | :-- | :-- |
| `client/src/locales/en/notifications.json` | “{{product}}”, which you saved, is back in stock | “{{product}}”, which you saved, is available again |
| `client/src/locales/vi/notifications.json` | “{{product}}” bạn đã lưu đã có hàng trở lại | “{{product}}” bạn đã lưu đã có thể mua lại |
| `admin.json` label (en / vi) | Saved product back in stock / Sản phẩm đã lưu có hàng lại | Saved product available again / Sản phẩm đã lưu có thể mua lại |
| Email subject (en / vi) | {product} is back in stock / {product} đã có hàng trở lại | {product} is available again / {product} đã có thể mua lại |
| Email body sentence | …, which you saved, is back in stock. | …, which you saved, is available again. |

Wording an administrator has saved over the defaults (specs/077, 078) is not changed. Placeholders are unchanged.
