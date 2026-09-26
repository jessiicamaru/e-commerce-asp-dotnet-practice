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
| 49 | Revoked access tokens are refused from an in-memory list in every service, fed by an event, rather than by shorter tokens or a lookup per request | Seconds rather than minutes at the cost of one small dictionary; it fails open to the old 15 minutes, never closed | [specs/065](../../specs/065-revoke-access-tokens/) |
| 50 | Returns are whole parcels within 7 days of delivery, and a seller's money is due only after that window - a hold, never a debt; the refund is goods plus tax, not delivery | No clawback of a payout, no negative balances; one refund and one restock per parcel | [specs/066](../../specs/066-parcel-returns/) |
| 51 | A seller's revenue is their own lines before tax on sold orders, less any part returned and refunded; the shop's rating is weighted by each product's review count | One seller's figures agree with their earnings; the admin Overview still counts returned sales, a known limit | [specs/068](../../specs/068-seller-insights/) |
| 52 | A voucher is composed (conditions, targets, amounts per currency) rather than special-cased; a shop voucher is paid by its seller out of their payout, a platform voucher and free delivery by the shop; tax is on the discounted price | One model for every case the user named; a seller's terms move only with their own vouchers; a refund is exactly what was paid | [specs/069](../../specs/069-vouchers/) |
| 53 | A sale is dated by when it was paid (`orders.PaidAt`, written by the settlement's guarded statement), falling back to when it was placed for older orders | An order placed at 23:59 and paid at 00:01 was revenue of the day before it was paid | [specs/072](../../specs/072-revenue-paid-day/) |
| 54 | Every test script removes what it made, even when it fails; the cleaner for older debris keeps what `cameras.json` names and deletes the rest | 97 test products against 14 cameras; a keep list cannot miss a new kind of debris, a delete pattern can | [specs/073](../../specs/073-test-debris/) |
| 55 | Search uses trigram indexes over an IMMUTABLE `unaccent` wrapper, with translations matched as a UNION of ids rather than `OR EXISTS` | 452 ms to 1.2 ms on 100,000 products; the `OR EXISTS` shape kept every product scanned even with the indexes | [specs/074](../../specs/074-search-index/) |
| 56 | A saver is told a product is back in stock only on the availability rollup's own false-to-true flip | Inventory announces per variant and often; only none-to-some is news | [specs/075](../../specs/075-saved-products/) |
| 57 | Only a product's seller answers its questions (staff for the shop's own); an administrator on a seller's product gets the same 404 | An answer is published as the seller's words, so moderation's "an administrator passes every check" does not apply | [specs/076](../../specs/076-product-questions/) |
| 58 | An email's words are append-only versions, allow-list sanitised on save and on every send, with values escaped as they are filled in | A stale editor is a 409 rather than a lost edit; a sanitiser fixed later protects versions saved before it | [specs/077](../../specs/077-email-templates/) |
| 59 | Notice wording edits live in Activity and are laid over the storefront's bundled words, which stay the default | A new kind has words before anybody edits it, and a server that is down means the bundled words, never blank notices | [specs/078](../../specs/078-notification-wording/) |
| 60 | Product images live in an S3-compatible bucket every Catalog instance shares (SeaweedFS), a key stored only when new (`If-None-Match: *`) | A directory assumed one instance and made the orphan reclaim destructive on two; MinIO no longer publishes community images | [specs/079](../../specs/079-object-storage/) |
| 61 | Playwright drives the storefront against the real stack, in Edge locally and Chromium in CI | Unit tests cannot see what only a browser does (a toast lost on remount was the first find); Edge needs no download on the developer's machine (the user's choice) | [specs/080](../../specs/080-e2e-browser/) |
| 62 | The newest 30 `sha-` versions of each image are kept; the prune never deletes `:main`, an untagged version or a hand-pushed tag | A usable list of releases (Catalog had 99); anything that is not plainly an old release is left alone. Decided with the user | #165, [prune-images.sh](../../.github/scripts/prune-images.sh) |
| 63 | What hangs on a product off the shelf answers like the product; its image needs a per-image key in the address, not a signed expiring URL | An `<img>` request carries no token; a key on the row needs no secret and every instance agrees. It cannot revoke an address already handed out without replacing the image | [specs/081](../../specs/081-unlisted-product-reads/) |
| 64 | Every insight counts the shop's days in one configured time zone (`Asia/Ho_Chi_Minh`), not UTC and not the reader's | With UTC days an order paid at 06:30 in Hanoi was yesterday's; one zone so two people see the same numbers | [specs/082](../../specs/082-insights-local-days/) |
| 65 | An email about an order is written in the order's language; any other in the language the reader last used the shop in, learnt at sign-up, sign-in and renewal rather than set | A setting is a screen nobody visits; a saved product coming back has no request of the reader's to take a language from | [specs/083](../../specs/083-more-emails/) |
| 66 | A parcel returned and received leaves the admin's revenue, top products and top buyers, dated on the day the order was paid; the order is still one order | The seller's page already left it out, and two screens disagreeing about one sale read as a bug; the delivery was kept | [specs/084](../../specs/084-admin-revenue-returns/) |
