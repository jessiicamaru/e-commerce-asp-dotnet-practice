# Glossary

Terms as this project uses them. Where a word has a precise meaning in the code, the code's name is
given in `code style`.

## Commerce

| Term | Meaning |
| :-- | :-- |
| **Product** | What a shopper sees: a name, a description, a category, a photograph, a "from" price. Not what is bought. |
| **Variant** | One shape a product is sold in - a kit, a mount, a colour, a capacity (`ProductVariant`). The variant is what is bought, priced and stocked; it carries the SKU. The first variant of a product reuses the product's id. |
| **Option** | One axis of a variant, e.g. `Kit: Body only` or `Mount: Sony E` (`VariantOption`). |
| **Price list** | The prices in one currency. There are two, VND and USD, set separately and never converted. A variant with no price in the requested currency is not for sale in it. |
| **The shop itself** | Products listed by an administrator rather than a seller (`SellerId` null). Also the part of an order made of those products. |
| **Seller** | An account holding the `Seller` role, which owns products (`products.SellerId`). A seller is always a customer too. |
| **Shop** | A seller's trading name and profile (`seller_profiles`). |
| **Shop application** | A request to become a seller, waiting for staff approval (`shop_applications`). |
| **Cart** | What a customer intends to buy; one per customer, holding variants and quantities but no prices. |
| **Checkout** | Turning the cart into an order: prices from Catalog, the address from Identity, the cart from Cart, all asked at that moment and frozen onto the order. |
| **Quote** | What checkout would charge for the same choices, computed by the same code and placing nothing (`GET /api/orders/quote`). |
| **Frozen** | Copied onto an order at checkout and never changed: price, product name, option summary, seller, shop name, address, language, currency, commission rate. An order is a record of a purchase, not a view of the catalogue. |
| **Part** / **parcel** | One seller's share of an order, shipped by that seller (`order_shipments`); the shop's own goods are one more part. |
| **Sale** | An order line of a seller's product on a paid order, as that seller sees it. |
| **Delivered** | A parcel the customer confirmed as arrived, or that was taken as arrived a week after shipping (`DeliveredAt`). Not a status. |
| **Commission** | The marketplace's cut of a seller's goods, before tax, at a rate frozen on the order. |
| **Payout** | A recorded settlement of what the shop owes one seller in one currency (`payouts`). Payment is a stub, so it is a ledger entry, not a transfer. |
| **On the way / due / paid out** | A seller's money on a paid order not yet delivered / delivered but not settled / settled by a payout. |
| **Cancellation** | Ending a paid order before it ships; its stock is returned and a refund recorded. Whole orders only. |
| **Review status** | Where a seller's product stands with the moderators: `Pending`, `Approved` or `Rejected`. Only approved products are listed and sellable. |
| **Review** (of a product) | A customer's 1-5 star rating and optional text, allowed only after receiving the product. Not to be confused with the review *status* above. |

## People and access

| Term | Meaning |
| :-- | :-- |
| **Role** | `Customer`, `Seller`, `Moderator` or `Admin`, carried in the access token. |
| **Staff** | Administrators and moderators (`StaffRoles.Staff`). |
| **Lock** | A time-limited stop on an account, set by staff with a reason. |
| **Ban** | An indefinite stop on an account, set and lifted only by an administrator. |
| **Access token** | A short-lived JWT (15 minutes) the storefront keeps in memory and sends as `Bearer`. |
| **Refresh token** | A long-lived, rotating token in an HttpOnly cookie that issues new access tokens; a used one presented again ends every session. |
| **Not yours = not found** | The rule that somebody else's product, sale, order or notification answers 404, never 403 - a 403 would confirm it exists. |

## Architecture

| Term | Meaning |
| :-- | :-- |
| **Service** | One deployable process owning one database: Identity, Catalog, Cart, Order, Inventory, Payment, Orchestrator, Activity. |
| **Gateway** | The YARP reverse proxy on port 5000; the only address the storefront uses. |
| **Saga** | The checkout's multi-service workflow, driven by the Orchestrator's state machine: reserve stock, take payment, complete or compensate. |
| **Compensation** | Undoing an earlier saga step when a later one fails - releasing reserved stock when payment is refused. |
| **Transactional outbox** | Writing a message to be published into the same database transaction as the change it announces; MassTransit delivers it afterwards. |
| **Inbox** | MassTransit's record of messages already consumed, so a redelivered one is not processed twice. |
| **Stage callback** | A repository method that runs a guarded statement in its own transaction takes a `stage` function, called inside that transaction, where the caller publishes its audit entry, notification or event - so they commit with the change. |
| **Guarded update** | A single `UPDATE ... WHERE <expected state>` whose affected-row count decides who won, instead of reading and then writing. |
| **Read model** | A copy of another service's data kept for display, fed by its events: shop names in Catalog, stock availability in Catalog, review eligibility in Catalog. Seconds behind by design; never used to decide a permission. |
| **Idempotent** | Handling the same message or request twice has the same effect as handling it once. |
| **h2c** | HTTP/2 without TLS, which gRPC between services uses on a separate port. |
| **Expand then contract** | Changing a schema in two releases (add, then remove later) so a previous image can still run against it. |

## Process

| Term | Meaning |
| :-- | :-- |
| **Spec** | A feature's design record under `specs/NNN-name/`: `spec.md` (what and why), `plan.md` (how, with a constitution check), `research.md` (decisions and rejected alternatives), `tasks.md`. |
| **Constitution** | The ratified principles every design is checked against (`.specify/memory/constitution.md`). |
| **Mutation check** | Deliberately breaking the rule a new test guards and confirming the test fails. |
| **Bruno** | The API collection under `bruno/` that exercises every endpoint through the gateway. |
| **Debris** | Products and accounts left behind by test runs against a shared development stack. |
