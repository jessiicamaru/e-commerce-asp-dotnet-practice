# HTTP API

> **Generated** by [`docs/tools/generate_reference.py`](../tools/generate_reference.py) from commit `532210d`. Do not edit by hand - change the code and run the script again.

Every endpoint a service exposes, grouped by service. Paths are the service's own; the gateway forwards `/api/...` to them unchanged (see [gateway.md](gateway.md)). **Who** is what the controller attributes allow - the handler may refuse further (somebody else's product is a 404, not a 403, for example); the feature documents say where.

**109 endpoints** across 7 services.

## Identity (29)

| Method | Path | Who | What |
| :-- | :-- | :-- | :-- |
| `GET` | `/api/addresses` | signed in |  |
| `POST` | `/api/addresses` | signed in |  |
| `DELETE` | `/api/addresses/{id}` | signed in |  |
| `GET` | `/api/addresses/{id}` | signed in |  |
| `PUT` | `/api/addresses/{id}` | signed in |  |
| `PUT` | `/api/addresses/{id}/default` | signed in |  |
| `POST` | `/api/auth/forgot-password` | anyone | Asks for a link to choose a new password (specs/061). Always 202, whether or not the address has an account (#28). The email is written in the language the request comes in. |
| `POST` | `/api/auth/login` | anyone |  |
| `POST` | `/api/auth/logout` | anyone | Ends the session: the refresh token is deleted server-side and the cookie is cleared. Anonymous on purpose - an expired access token must not stop someone signing out. Always 204. |
| `POST` | `/api/auth/refresh` | anyone |  |
| `POST` | `/api/auth/register` | anyone |  |
| `POST` | `/api/auth/register-seller` | anyone | Registers somebody who sells, with the name their shop trades under (specs/027). |
| `POST` | `/api/auth/reset-password` | anyone | Chooses a new password with the link's token (specs/061); every session ends. 204, or 400. |
| `GET` | `/api/sellers/me` | Seller |  |
| `PUT` | `/api/sellers/me/shop-name` | Seller | Renames the caller's shop. No product is written - the catalogue keeps the name as a read model, so two hundred listings change because one row did. |
| `GET` | `/api/shop-applications` | Admin, Moderator |  |
| `POST` | `/api/shop-applications` | Customer |  |
| `GET` | `/api/shop-applications/mine` | signed in |  |
| `POST` | `/api/shop-applications/{id}/approve` | Admin, Moderator |  |
| `POST` | `/api/shop-applications/{id}/reject` | Admin, Moderator |  |
| `GET` | `/api/users` | Admin, Moderator |  |
| `GET` | `/api/users/lookup` | Admin | Who these ids are - for the insights' top buyers (specs/047). Administrators only. |
| `GET` | `/api/users/stats` | Admin |  |
| `POST` | `/api/users/{id}/ban` | Admin |  |
| `POST` | `/api/users/{id}/lock` | Admin, Moderator |  |
| `DELETE` | `/api/users/{id}/roles/{role}` | Admin |  |
| `PUT` | `/api/users/{id}/roles/{role}` | Admin |  |
| `POST` | `/api/users/{id}/unban` | Admin |  |
| `POST` | `/api/users/{id}/unlock` | Admin, Moderator |  |

## Catalog (38)

| Method | Path | Who | What |
| :-- | :-- | :-- | :-- |
| `GET` | `/api/categories` | anyone |  |
| `POST` | `/api/categories` | Admin |  |
| `DELETE` | `/api/categories/{id}` | Admin | Removes a category nothing is filed under (specs/024). A category with products in it is refused with 409 rather than emptied: neither orphaning them nor deleting them is what somebody tidying a taxonomy asked for. |
| `DELETE` | `/api/categories/{id}/translations/{language}` | Admin | Takes a language away; the category falls back to its default text. |
| `PUT` | `/api/categories/{id}/translations/{language}` | Admin | This category's name and description in one language (specs/026). An upsert, like a product's. |
| `GET` | `/api/products` | anyone |  |
| `POST` | `/api/products` | Seller, Admin | Lists a product. A seller's product is theirs; an administrator's belongs to the shop itself (specs/027) - who it belongs to comes from the token, never from the body. |
| `DELETE` | `/api/products/images/orphans` | Admin | Reclaims them, and answers with what it removed. |
| `GET` | `/api/products/images/orphans` | Admin | What the image store holds that the catalogue cannot name (specs/033). Changes nothing. |
| `GET` | `/api/products/insights/top-viewed` | Admin |  |
| `GET` | `/api/products/mine` | Seller | The caller's own listings, and only theirs (specs/027). Takes no seller id. |
| `GET` | `/api/products/review` | Admin, Moderator | The moderators' queue (Pending, oldest first) or the history of one status. |
| `DELETE` | `/api/products/{id}` | Seller, Admin | Removes a product and every shape of it from the catalogue, for good (specs/024). |
| `GET` | `/api/products/{id}` | anyone |  |
| `POST` | `/api/products/{id}/approve` | Admin, Moderator |  |
| `DELETE` | `/api/products/{id}/image` | Seller, Admin |  |
| `GET` | `/api/products/{id}/image` | anyone | A product's image, for anyone. Cacheable for good only when v names the current version - the address changes with the image, so that is safe (specs/019 D6). |
| `PUT` | `/api/products/{id}/image` | Seller, Admin | Give a product its image, or replace it (specs/019). One multipart part named file; JPEG, PNG or WebP by content, at most 2 MB. |
| `PUT` | `/api/products/{id}/options/{optionId}/translations/{language}` | Seller, Admin | One option in one language - Kit: Body only → Bộ: Chỉ thân máy. Option values are read by customers as much as names are. |
| `POST` | `/api/products/{id}/reject` | Admin, Moderator |  |
| `POST` | `/api/products/{id}/resubmit` | Seller, Admin | Sends a rejected product back to the queue. The caller's own - somebody else's is 404. |
| `POST` | `/api/products/{id}/take-down` | Admin, Moderator |  |
| `DELETE` | `/api/products/{id}/translations/{language}` | Seller, Admin | Takes a language away; the product falls back to its default text. |
| `PUT` | `/api/products/{id}/translations/{language}` | Seller, Admin | This product's name and description in one language (specs/021). An upsert: writing it twice leaves the second text, not a conflict. |
| `POST` | `/api/products/{id}/variants` | Seller, Admin | Another shape of the product: a kit, a colour, a size (specs/020). The variant carries the sku and the price, and it is what a customer actually buys. |
| `PUT` | `/api/products/{id}/variants/{variantId}` | Seller, Admin | Re-prices a variant or takes it off sale. The sku and the options never change: an order froze them, and it has to keep describing what was bought. |
| `DELETE` | `/api/products/{id}/variants/{variantId}/image` | Seller, Admin | Take one shape's own photograph away. It then falls back to the product's. |
| `GET` | `/api/products/{id}/variants/{variantId}/image` | anyone | One shape's own photograph, for anyone. 404 when it has none of its own - callers are given the product's address by VariantResponse.ImageUrl and should not be here. |
| `PUT` | `/api/products/{id}/variants/{variantId}/image` | Seller, Admin | Give one SHAPE of a product its own photograph, or replace it (specs/032). |
| `DELETE` | `/api/products/{id}/variants/{variantId}/prices/{currency}` | Seller, Admin | Stops selling this variant in this currency. It is then reported with no price rather than with a converted one. Refused for the default currency, which has no row to remove. |
| `PUT` | `/api/products/{id}/variants/{variantId}/prices/{currency}` | Seller, Admin | What this variant costs in one currency (specs/022). An upsert, like a translation. |
| `POST` | `/api/products/{id}/view` | anyone | The product page was opened. Anonymous, and always 204 - counted or not. |
| `GET` | `/api/products/{productId}/reviews` | anyone |  |
| `GET` | `/api/products/{productId}/reviews/mine` | signed in |  |
| `PUT` | `/api/products/{productId}/reviews/mine` | Customer |  |
| `GET` | `/api/reviews` | Admin, Moderator |  |
| `POST` | `/api/reviews/{id}/hide` | Admin, Moderator |  |
| `POST` | `/api/reviews/{id}/restore` | Admin, Moderator |  |

## Cart (5)

| Method | Path | Who | What |
| :-- | :-- | :-- | :-- |
| `DELETE` | `/api/cart` | signed in |  |
| `GET` | `/api/cart` | signed in |  |
| `POST` | `/api/cart/items` | signed in |  |
| `DELETE` | `/api/cart/items/{productId}` | signed in |  |
| `PUT` | `/api/cart/items/{productId}` | signed in |  |

## Order (23)

| Method | Path | Who | What |
| :-- | :-- | :-- | :-- |
| `GET` | `/api/orders` | signed in | The caller's own orders, newest first. Neither this nor accepts a user id — see . |
| `POST` | `/api/orders` | signed in | Check out the caller's cart to one of their addresses (feature 011). |
| `GET` | `/api/orders/fulfilment` | Admin | Staff: every customer's orders in one fulfilment status - Paid, Preparing or Shipped. |
| `GET` | `/api/orders/fulfilment/{id}` | Admin | Staff: any order's detail, so they can see what to pack and where it goes (specs/038). The one read of an order that is not scoped to its owner - the role is the permission. |
| `POST` | `/api/orders/fulfilment/{id}/cancel` | Admin | Staff: cancel any paid order until its first parcel has shipped (specs/039). |
| `GET` | `/api/orders/insights/revenue` | Admin |  |
| `GET` | `/api/orders/insights/top-buyers` | Admin |  |
| `GET` | `/api/orders/insights/top-products` | Admin |  |
| `POST` | `/api/orders/payouts` | Admin | Staff: settle everything due to one seller in one currency. 201 with the payout; 409 when nothing is due - including when another administrator has just settled it. |
| `GET` | `/api/orders/payouts/due` | Admin | Staff: every seller with something due now, per currency. |
| `GET` | `/api/orders/quote` | signed in | What checking out would cost now - the same parts, computed by the same code, as the order the same choices would place (#38). Places nothing. |
| `GET` | `/api/orders/sales` | Seller | A seller's sales: paid orders holding at least one of their lines, with figures over those lines only. Seller, not Admin - an administrator sees every order through fulfilment already. |
| `GET` | `/api/orders/sales/balance` | Seller | A seller's money per currency: on the way, due, paid out. |
| `GET` | `/api/orders/sales/payouts` | Seller | The payouts made to a seller, newest first. |
| `GET` | `/api/orders/sales/{id}` | Seller | One sale, the seller's own lines only. 404 - one wording - for no such order, nothing of theirs on it, failed, or still settling. |
| `POST` | `/api/orders/sales/{id}/preparing` | Seller | A seller starts preparing THEIR part of this order (specs/035). 404 - one wording - when it is not their sale, not there, not paid or failed; 409 when their part is not waiting. |
| `POST` | `/api/orders/sales/{id}/shipment` | Seller | A seller has sent THEIR part, with a tracking reference. Repeating it is a no-op. |
| `GET` | `/api/orders/shipping-options` | anyone | The delivery options and what each costs. Public - prices are not a secret. |
| `GET` | `/api/orders/{id}` | signed in | One of the caller's own orders. Answers 404 both when the order does not exist and when it belongs to another shopper. |
| `POST` | `/api/orders/{id}/cancel` | signed in | The customer cancels their own paid order while every parcel is still waiting (specs/039). 404 for none or not theirs; 409 once anything is being prepared or shipped; repeating it is a no-op. |
| `POST` | `/api/orders/{id}/preparing` | Admin | Staff: Paid → Preparing. Repeating it is a no-op; from any other state, 409. |
| `POST` | `/api/orders/{id}/shipment` | Admin | Staff: Preparing → Shipped, with a tracking reference. |
| `POST` | `/api/orders/{id}/shipments/{shipmentId}/received` | signed in | The customer says one parcel of their order arrived (specs/040). 404 for none or not theirs; 409 if it has not been shipped; repeating it is a no-op. |

## Inventory (4)

| Method | Path | Who | What |
| :-- | :-- | :-- | :-- |
| `GET` | `/api/reservations/{orderId}` | Admin |  |
| `GET` | `/api/stock` | anyone |  |
| `GET` | `/api/stock/{productId}` | anyone | Stock for one sellable unit. Since specs/020 the id is a variant id - what a customer actually buys. For every product that existed before variants, its id is also its only variant's id, so an old link still works. |
| `PUT` | `/api/stock/{productId}` | Seller, Admin | Sets stock for one sellable unit; the id is a variant id (specs/020). |

## Payment (2)

| Method | Path | Who | What |
| :-- | :-- | :-- | :-- |
| `GET` | `/api/payments` | Admin |  |
| `GET` | `/api/payments/{orderId}` | Admin |  |

## Activity (8)

| Method | Path | Who | What |
| :-- | :-- | :-- | :-- |
| `GET` | `/api/audit` | Admin | A page of entries, newest first, filtered by category, action, actor, subject and period. |
| `GET` | `/api/audit/mine` | Admin, Moderator |  |
| `GET` | `/api/audit/summary` | Admin | How many entries each category holds in a period. |
| `GET` | `/api/audit/{id}` | Admin | One entry, with its snapshots and field-level diff. |
| `GET` | `/api/notifications` | signed in |  |
| `POST` | `/api/notifications/read-all` | signed in |  |
| `GET` | `/api/notifications/unread-count` | signed in | The bell's number - polled, so it is kept to one count. |
| `POST` | `/api/notifications/{id}/read` | signed in |  |
