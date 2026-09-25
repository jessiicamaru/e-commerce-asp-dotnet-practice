# Decision log

The design decisions that shape the system, one line each, with where the reasoning is recorded. Each
design record (`specs/NNN/research.md`) also lists the alternatives that were rejected and why; the two
formal ADRs are in [architecture](../architecture/). A decision here is only reversed by a later design
record that says so.

## Architecture and data

| # | Decision | Why | Recorded in |
| :-- | :-- | :-- | :-- |
| 1 | One PostgreSQL database per service; no service reads another's tables | Service autonomy - a schema change in one service cannot break another | [constitution I](../../.specify/memory/constitution.md), [microservices design](../architecture/microservices-design.md) |
| 2 | Primary keys are time-ordered UUID v7 | Globally unique across services without a round trip, and index-friendly | [ADR-001](../architecture/adr-001-uuidv7-primary-keys.md) |
| 3 | Every publish goes through the transactional outbox, before the one save | A change without its message, or a message without its change, loses money or stock | [reliable messaging](../architecture/reliable-messaging-and-outbox-pattern.md) |
| 4 | A checkout saga in its own Orchestrator service, not choreography | One place that knows the whole workflow and its compensation | [saga roadmap](../architecture/saga-orchestration-roadmap.md) |
| 5 | Synchronous calls only where a request needs the answer now, over gRPC on a second port | Everything else tolerates delay; one plaintext port cannot carry both HTTP/1.1 and HTTP/2 | [service-to-service](../architecture/service-to-service-communication.md), [specs/009](../../specs/009-catalog-owns-price/) |
| 6 | No new enum value a previous image cannot parse; delivered, review status and shipments as columns or text | A rollback must not take a service down | [specs/035](../../specs/035-seller-shipments/), [specs/040](../../specs/040-delivery-confirmation/), [specs/045](../../specs/045-product-review/) |
| 7 | Schema changes expand then contract | An earlier image must run against the newer schema | [specs/006](../../specs/006-release-and-rollback/) |
| 8 | Released images are named by commit, and a name never changes | "The previous version" must mean one thing | [specs/008](../../specs/008-immutable-release-tags/) |

## Money and orders

| # | Decision | Why | Recorded in |
| :-- | :-- | :-- | :-- |
| 9 | Catalog owns the price; the order freezes it at checkout | The customer once set the price in the request body | [specs/009](../../specs/009-catalog-owns-price/) |
| 10 | Prices exclude tax; tax by destination, per line and on delivery, rounded half away from zero | Different countries, one price list | [ADR-002](../architecture/adr-002-tax-exclusive-prices.md) |
| 11 | Totals stored in named parts with a CHECK that they add up | A total with nothing behind it cannot be explained or audited | [specs/012](../../specs/012-order-totals/) |
| 12 | Two price lists set separately, never converted; a missing price is not for sale | Converting sells a camera for the wrong amount; falling back is worse | [specs/022](../../specs/022-multi-currency-prices/) |
| 13 | Money is never added across currencies, in reports too | The sum of dong and dollars is neither | [specs/047](../../specs/047-admin-insights/) |
| 14 | The quote and the order are priced by the same code | What is shown must be what is charged | [specs/012](../../specs/012-order-totals/) |
| 15 | The cart removes what was ordered only when the order completes | A declined card must not empty the cart | [specs/010](../../specs/010-customer-cart/) |
| 16 | Stock is reserved under a row lock on one aggregated row, not a unit pool | Simpler, and enough at this scale | [specs/001](../../specs/001-inventory-reservations/), [SKIP LOCKED study](../concepts/shopify-inventory-skip-locked-pattern.md) |
| 17 | Commission is one marketplace rate frozen per order; delivery split equally between parts | A rate changed later must not rewrite old orders | [specs/037](../../specs/037-seller-payouts/) |
| 18 | A seller is owed money only for delivered parcels | Paid for what arrived, not for what was said to be sent | [specs/040](../../specs/040-delivery-confirmation/) |
| 19 | A payout claims its parts and records their sum in one statement | Two administrators paying at once must not pay twice | [specs/037](../../specs/037-seller-payouts/) |
| 20 | Cancellation is whole orders only, before the first parcel ships | Partial cancellation needs partial refunds and restocks | [specs/039](../../specs/039-order-cancellation/) |

## Marketplace and trust

| # | Decision | Why | Recorded in |
| :-- | :-- | :-- | :-- |
| 21 | Somebody else's resource is a 404, never a 403 | A 403 confirms the id exists and belongs to someone | [specs/027](../../specs/027-seller-accounts/) |
| 22 | Who owns a variant is asked live when stocking, never cached | Authorization must not be eventually consistent | [specs/031](../../specs/031-seller-stock/) |
| 23 | The seller of an order line is frozen at checkout | A sale records who owned the product then; stock asks who owns it now | [specs/034](../../specs/034-seller-sales/) |
| 24 | A new shop and a seller's new product wait for staff approval | A marketplace checks who sells and what is on its shelves | [specs/044](../../specs/044-shop-applications/), [specs/045](../../specs/045-product-review/) |
| 25 | Editing an approved product's words or photographs sends it back to review; prices and stock do not | Decided with the user: what a shopper reads is what was approved | [specs/045](../../specs/045-product-review/) |
| 26 | Moderator is the only grantable role; moderators lock for at most 30 days, only administrators ban | A second bootstrap path for Admin is a hole; stopping a moderator is an administrator's call | [specs/043](../../specs/043-moderators-and-locks/) |
| 27 | A locked account is told why only after the right password | Before it, a locked account and a wrong password must look the same | [specs/043](../../specs/043-moderators-and-locks/) |
| 28 | Only a customer who received a product reviews it; eligibility fed by Order's delivery event | Catalog does not know who bought what; asking Order live would add a dependency | [specs/046](../../specs/046-product-reviews/) |
| 29 | Ratings are recomputed from visible reviews, never incremented; hidden, never deleted | Increments drift under concurrency; a moderator can be wrong | [specs/046](../../specs/046-product-reviews/) |

## People, language and records

| # | Decision | Why | Recorded in |
| :-- | :-- | :-- | :-- |
| 30 | The user id never comes from a request body | The same defect as a price in the body, one field over | [constitution IV](../../.specify/memory/constitution.md) |
| 31 | Emails are compared case-insensitively and stored as typed | One mailbox, one account | issue #49 |
| 32 | A used refresh token presented again ends every session | Two parties hold it; the server cannot tell which is the owner | issue #29 |
| 33 | An order freezes its language | A Vietnamese order still reads Vietnamese when opened in English | [specs/021](../../specs/021-internationalisation/) |
| 34 | A notification stores a kind and data, never a sentence | It is worded in whatever language the reader has now | [specs/042](../../specs/042-in-app-notifications/) |
| 35 | Every service records an audit entry through its outbox, redacted before it leaves | The entry commits with its change; secrets never reach the log | [specs/041](../../specs/041-audit-log/) |
| 36 | Orphaned images are reclaimed only when a person asks | Its failure mode is deleting images somebody is using | [specs/033](../../specs/033-image-reconciliation/) |
| 37 | A variant's photograph falls back to the product's on the server | A second place to decide the fallback is a second place to get it wrong | [specs/032](../../specs/032-variant-images/) |
| 38 | The storefront draws by role but never decides by role | Every rule it shows is enforced by the server on its own | [specs/028](../../specs/028-seller-console/) |
| 39 | A notification kind's data keys are declared once, and checked by tests on both sides - never at run time | A wording mismatch that threw would roll back the payout or decision it announces; five kinds had shown `{{placeholders}}` because nothing saw both sides | [specs/048](../../specs/048-notification-wording/) |
| 40 | A 403 carries its facts beside its sentence, and the client words them | A reader deserves the reason in their language and time zone, as notifications are worded from data | [specs/049](../../specs/049-sign-in-refusal/) |
| 41 | A moderator lifts only a lock with at most 30 days to run - read from the time left, not who set it | No column or migration, and a long lock stays until an administrator decides | [specs/050](../../specs/050-unlock-rules/) |
| 42 | The storefront image is nginx forwarding `/api` to a gateway named at start | One origin as in development, one image for any gateway, no .NET image tied to a Node build | [specs/051](../../specs/051-storefront-image/) |
| 43 | The saga's payment timeout is a sweeper over the saga table, shorter than the stock hold, and a late approval is refunded | Durable `Schedule` needs a RabbitMQ plugin nobody has; failing while the stock is still held means nothing is ever paid for stock back on the shelf | [specs/053](../../specs/053-saga-payment-timeout/) |
| 44 | Email is sent by Identity from a durable queue, not by the service that asks nor by a retrying consumer | Identity alone knows addresses; a row with its own next attempt survives a mail server that is down and a restart | [specs/060](../../specs/060-email/) |
| 45 | A reset link's token is stored only as a hash, its email is queued by Identity directly rather than through the broker, and the sent row is scrubbed | The token is a credential: nothing that outlives its delivery - a table, an outbox row, a queue, a sent email's data - may hold it | [specs/061](../../specs/061-password-reset/) |
| 46 | Wrong passwords pause sign-in per email for 5 minutes, not per account and not with a lock; the gateway limits per IP and believes X-Forwarded-For only from the storefront | Per account would answer unknown emails differently (#28); a lock anybody can trigger shuts anybody out; a header the caller writes is no limit | [specs/062](../../specs/062-auth-rate-limits/) |
| 47 | An unconfirmed address may buy but not open a shop; the approval waits for it, not the seller registration; accounts from before count as confirmed | A shop is a public claim in the address's name, a purchase costs the address's owner nothing; asking existing sellers again would stop trading shops | [specs/063](../../specs/063-email-confirmation/) |
| 48 | A password change keeps the session that made it (named by its HttpOnly cookie) and ends every other; a wrong current password counts toward the sign-in pause | The person who just proved the password should not be signed out; a stolen access token must not be a faster way to guess it | [specs/064](../../specs/064-account-settings/) |
