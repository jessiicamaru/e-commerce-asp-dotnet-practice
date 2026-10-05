# Project timeline

How the system was built, in the order it was built. Each row names the design record (`specs/`), the
pull request that merged it, and what it added. The design records hold the reasoning - what was
decided, what was rejected and why - and are the place to go for any "why is it like this?" question.

The work was done in short feature cycles: a specification (`spec.md`), a plan with research and a
constitution check (`plan.md`, `research.md`), a task list (`tasks.md`), then the implementation, tests,
the Bruno requests and a pull request whose CI must pass before it is squash-merged. The ratified
principles every design is checked against are in the
[constitution](../../.specify/memory/constitution.md).

Repository: <https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice>

## At a glance

| Period | Theme | Specs | Outcome |
| :-- | :-- | :-- | :-- |
| 2026-08-31 - 09-03 | Foundations | - | Identity with JWT and refresh tokens, Catalog, YARP gateway, the shared building block, the outbox, the Orchestrator and Order services |
| 2026-09-16 - 09-17 | A checkout that finishes | 001 - 004 | Inventory reservations, a stub Payment service, orders that settle, one source of truth for stock |
| 2026-09-17 - 09-21 | Releasable and verifiable | 005 - 008 | Containers, published immutable images, schema-compatibility checks, an end-to-end saga check in CI |
| 2026-09-21 - 09-22 | A shop you can trust | 009 - 013 | Prices owned by Catalog (a fixed price-tampering hole), a cart, delivery addresses and shipping, totals with tax, tracing across services |
| 2026-09-21 - 09-22 | Security fixes | issues #28 - #49 | Correct status codes, registration validation, startup refusal on bad JWT settings, refresh-token reuse detection, one account per mailbox |
| 2026-09-22 | A storefront | 014 - 018, 025 | React client: auth, catalogue, cart and addresses, checkout and order history |
| 2026-09-22 | A richer catalogue | 019 - 024, 026 | Images, variants, Vietnamese and English, two price lists, real camera seed data, product deletion, translated categories |
| 2026-09-22 - 09-23 | A marketplace | 027 - 037 | Seller accounts and console, seller stock, variant photos, image reconciliation, seller sales, per-seller shipments, shop names on parcels, commission and payouts |
| 2026-09-23 - 09-24 | Running the shop | 038 - 040 | Admin console, order cancellation with restock and refund, delivery confirmation |
| 2026-09-23 - 09-24 | Staff and trust | 041 - 047 | Audit log, in-app notifications, moderators and account locks, shop applications, product review before sale, ratings and reviews, admin insights |

## Foundations (2026-08-31 - 2026-09-03)

Committed directly to `main`, before the feature-cycle process. In order:

- Identity: entities, EF Core with Fluent API, password hashing, JWT signing, MediatR commands for
  register and login, cookie-based rolling refresh tokens.
- RabbitMQ in Docker; the monorepo with the YARP gateway, Identity and Catalog, one database each.
- Catalog: categories and products, queries with paging, sorting and filtering; validators.
- `Ecommerce.Shared`: the global exception handler (RFC 7807) and the validation pipeline behaviour.
- The transactional outbox, and the fix that made publishing atomic with the write.
- The Orchestrator (a MassTransit saga state machine) and the Order service.
- Architecture notes still in `docs/`: the saga roadmap, reliable messaging, ADR-001 (UUID v7 keys),
  PACELC trade-offs, and a study of Shopify's `SKIP LOCKED` reservation design (not adopted).

## Feature by feature

| Spec | Title | PR | What it added |
| :-- | :-- | :-- | :-- |
| [001](../../specs/001-inventory-reservations/) | Inventory reservations | (main) | The Inventory service; stock reserved under a row lock, released or confirmed, an expiry sweeper |
| [002](../../specs/002-payment-service/) | Payment service | #1 | A Payment service with a stub gateway that moves no money; the saga runs end to end |
| [003](../../specs/003-order-lifecycle/) | Order lifecycle visibility | #3 | Order settles on the saga's outcome with a guarded update; orders readable by their owner |
| [004](../../specs/004-stock-single-source/) | One source of truth for stock | #5 | Catalog's availability becomes a read model fed by Inventory; nothing sells against it |
| [005](../../specs/005-containerise-services/) | Run the system in containers | #10 | One Dockerfile for every service, configured from the environment |
| [006](../../specs/006-release-and-rollback/) | Releasable versions and rollbacks | #11 | Images published to GHCR by commit; a CI comment on migrations that would strand an older image |
| [007](../../specs/007-saga-e2e-verification/) | Checkout verified end to end | #13, #41 | `verify-saga.sh`: a real order through every service, stock asserted on hand and reserved |
| [008](../../specs/008-immutable-release-tags/) | An identifier that never changes | #17 | A `sha-` image tag can never be rewritten; the release checks it is whole |
| [009](../../specs/009-catalog-owns-price/) | The shop decides what things cost | #24 | Order asks Catalog for prices over gRPC and freezes them - closing a hole where the customer set the price |
| [010](../../specs/010-customer-cart/) | A cart | #25 | The Cart service; checkout reads the cart, not a request body |
| - | Bruno collection | #26, #27 | Every public endpoint, runnable and tested through the gateway |
| [011](../../specs/011-order-shipping/) | Somewhere for the order to go | #31 | Delivery addresses in Identity, shipping options, fulfilment to Shipped |
| [012](../../specs/012-order-totals/) | A total with something behind it | #32 | Totals stored as parts with a CHECK constraint; tax by destination ([ADR-002](../architecture/adr-002-tax-exclusive-prices.md)) |
| [013](../../specs/013-observability/) | Following one order | #33 | OpenTelemetry to Seq; one trace per checkout across HTTP, gRPC and the broker |
| - | Error codes (#28) | #40 | Wrong passwords and duplicate emails are 401/409, not 500 |
| - | Registration validation (#43) | #50 | Malformed emails, short passwords and empty names refused |
| - | JWT settings (#30) | #51 | A service with incomplete JWT settings refuses to start |
| - | Refresh-token reuse (#29) | #52 | A replayed refresh token ends every session of that user |
| - | One account per mailbox (#49) | #53 | Emails compared case-insensitively, unique on `lower(Email)` |
| [014](../../specs/014-storefront-scaffold/) | A storefront that builds | #42 | The React client, talking only to the gateway, in CI |
| [015](../../specs/015-storefront-auth/) | Sign up, sign in, stay signed in | #44 | Access token in memory, refresh token in an HttpOnly cookie |
| [016](../../specs/016-storefront-catalog/) | Browse, search and open a product | #46 | Catalogue pages |
| [017](../../specs/017-storefront-cart/) | A cart and an address book | #47 | Cart and address pages |
| [018](../../specs/018-storefront-checkout/) | Checkout and order history | #48 | Checkout that waits for the saga honestly; order history |
| - | Client stack | #55, #56 | Tailwind, shadcn/ui, axios and TanStack Query, by the agreed folder layout |
| [019](../../specs/019-product-images/) | Product images | #54 | Images on a volume behind `IProductImageStore`; type from the bytes |
| [020](../../specs/020-product-variants/) | Product variants | #57 | The variant is what is bought: its own SKU, price, options and stock |
| [021](../../specs/021-internationalisation/) | Speaking more than one language | #58 | Vietnamese and English interface and product text; orders freeze their language |
| [022](../../specs/022-multi-currency-prices/) | Two price lists | #59 | VND and USD prices set separately, never converted |
| [023](../../specs/023-camera-catalogue/) | Real camera catalogue | #60 | `server/seed/`: 14 cameras seeded through the API |
| [024](../../specs/024-delete-product/) | Delete a product | #61 | An administrator removes a product for good |
| [025](../../specs/025-storefront-redesign/) | A storefront that looks like a shop | #62 | Visual redesign |
| [026](../../specs/026-category-translations/) | Translated categories | #63 | Category names in both languages |
| [027](../../specs/027-seller-accounts/) | The shop is a marketplace | #64 | Sellers own products; somebody else's product is a 404 |
| [028](../../specs/028-seller-console/) | A seller can actually sell | #65 | The seller console; the client gets unit tests |
| [029](../../specs/029-delete-product-image/) | A deleted product takes its picture | #68 | Deleting a product deletes its image |
| [030](../../specs/030-seed-photographs/) | Seed photographs | #69 | `seed-images.py`, keeping the images out of the repository |
| [031](../../specs/031-seller-stock/) | A seller stocks what they sell | #71 | Inventory asks Catalog who owns a variant, live |
| [032](../../specs/032-variant-images/) | The picture follows the variant | #73 | A photograph per variant, falling back to the product's |
| [033](../../specs/033-image-reconciliation/) | Images nobody can name | #74 | Find and reclaim orphaned images, on request only |
| [034](../../specs/034-seller-sales/) | A seller sees what they sold | #77 | The seller frozen on each order line; a seller's own sales |
| - | Storefront and seller console redesign | #78 | Visual redesign |
| [035](../../specs/035-seller-shipments/) | Each seller ships their own part | #79 | One shipment part per seller per order, under the order's row lock |
| [036](../../specs/036-parcel-shop-names/) | Which shop each parcel comes from | #80 | Shop names frozen on order lines |
| [037](../../specs/037-seller-payouts/) | What the shop owes each seller | #81 | Commission frozen at checkout, delivery shares, a payout ledger |
| [038](../../specs/038-admin-console/) | An administrator's console | #82, #83 | Fulfilment queues and payouts in the storefront |
| [039](../../specs/039-order-cancellation/) | Cancelling a paid order | #84 | Whole-order cancellation; Inventory restocks and Payment records a refund |
| [040](../../specs/040-delivery-confirmation/) | Confirming a parcel arrived | #85 | Customer confirmation or automatic after 7 days; money due only when delivered |
| [041](../../specs/041-audit-log/) | An audit log | #93 | The Activity service; who did what, by category, with the diff |
| [042](../../specs/042-in-app-notifications/) | In-app notifications | #94 | A notification bell; each service tells people what happened to them |
| [043](../../specs/043-moderators-and-locks/) | Moderators, locks and bans | #95 | A Moderator role granted by email; account locks and bans |
| [044](../../specs/044-shop-applications/) | Shop applications | #96 | A new shop waits for staff approval |
| [045](../../specs/045-product-review/) | Product review before sale | #97 | A seller's product is hidden until a moderator approves it; the moderator dashboard |
| [046](../../specs/046-product-reviews/) | Ratings and reviews | #98 | Only somebody who received a product reviews it |
| [047](../../specs/047-admin-insights/) | Admin insights | #99, #101 | Revenue per currency, top selling, top viewed, top buyers |
| - | Fresh-container fix | #100 | Identity applies migrations before seeding, so an empty database starts |
| [048](../../specs/048-notification-wording/) | Notification wording | #129 | Fixes #119; each notification kind's data keys declared once and tested on both server and storefront |
| [049](../../specs/049-sign-in-refusal/) | Sign-in refusal | #130 | Fixes #120; a locked or banned person is told why and until when, in their language and time |
| [050](../../specs/050-unlock-rules/) | Unlock rules | #131 | Fixes #121; unlocking obeys the limits locking does |
| [051](../../specs/051-storefront-image/) | Storefront image | #133 | Fixes #132; the storefront is the tenth published image - nginx serving the bundle on :8088 and forwarding `/api` to the gateway |
| [052](../../specs/052-cart-removal-by-variant/) | Cart removal by variant | #134 | Fixes #122; a completed order takes out the variant it bought, not its sibling |
| [053](../../specs/053-saga-payment-timeout/) | Saga payment timeout | #135 | Fixes #123; the saga stops waiting for Payment before the stock hold expires, and refunds a late approval; the saga's first test project |
| - | OpenAPI advisory | #136 | `Microsoft.AspNetCore.OpenApi` 10.0.12, off a vulnerable `Microsoft.OpenApi` (GHSA-v5pm-xwqc-g5wc) |
| [054](../../specs/054-variant-availability-guard/) | Variant availability guard | #137 | Fixes #124; a variant's availability follows Inventory's latest word, whatever order it arrives in |
| [055](../../specs/055-insights-period/) | One insights period | #138 | Fixes #125; every insight counts the same whole UTC days, and the chart draws the days the totals count |
| [056](../../specs/056-review-on-every-seller-edit/) | Review on every seller edit | #139 | Fixes #126; translating an option or adding a variant sends an approved seller product back to review |
| [057](../../specs/057-review-races/) | Review races | #140 | Fixes #127; a double-posted review is one review, a review is hidden once, nobody reviews what they sell |
| [058](../../specs/058-audit-gaps/) | Audit gaps | #141 | #128 part A: the missing audit entries; a stale session after a lock is no longer taken for theft |
| [059](../../specs/059-missing-notices/) | The notices nobody got | #142 | Fixes #128 (part B): a sweep-delivered parcel, a lock, a ban and a hidden review each tell the person concerned |
| [060](../../specs/060-email/) | Email | #143 | Fixes #102: `IEmailSender`, Identity's outgoing queue and dispatcher, Mailpit; the order confirmation |
| [061](../../specs/061-password-reset/) | Password reset | #144 | Fixes #103: a single-use, hashed, 30-minute link by email; one guarded claim; every session ends; the storefront's two pages |
| [062](../../specs/062-auth-rate-limits/) | Limits on guessing and on email | #145 | Fixes #105: per-IP limits at the gateway (X-Forwarded-For only from the storefront), a 5-minute pause per email after 5 wrong passwords, one reset email a minute per address |
| [063](../../specs/063-email-confirmation/) | Email confirmation | #146 | Fixes #106: a single-use, hashed, 24-hour link on registering; a banner and "send it again"; no shop until the address is confirmed; existing accounts count as confirmed |
| [064](../../specs/064-account-settings/) | Account settings | #147 | Fixes #104: change your name, phone and password; the current password is checked and counted toward the sign-in pause; this session stays, every other ends |
| [065](../../specs/065-revoke-access-tokens/) | Access tokens revoked | #148 | Fixes #112: `AccessTokensRevoked` on a lock, ban, role revoked, password reset or change and token reuse; every service refuses earlier tokens within seconds |
| [066](../../specs/066-parcel-returns/) | Parcel returns (server) | #149 | #107 part 1: request within 7 days, the seller or staff decide, disputes, sent back, received; refund and restock once; money due only after the return window |
| [067](../../specs/067-return-screens/) | Parcel returns (screens) | #151 | #107 part 2: the buyer returns, sends back and escalates from the order page; the seller accepts, refuses and receives from the sale; staff settle disputes on the order and in `/admin/returns` |
| [068](../../specs/068-seller-insights/) | Seller insights | #152 | #111: a seller's own revenue per currency and day (their lines before tax, returns refunded left out), best sellers, views and a weighted rating, on `/shop/insights` |
| [069](../../specs/069-vouchers/) | Vouchers (server) | #153 | #108 part 1: platform and shop vouchers made of conditions, targets and amounts per currency; priced by the checkout's own code, tax after discount; uses claimed with the order and given back by a failed or cancelled one; the seller pays for their own |
| [070](../../specs/070-voucher-screens/) | Vouchers (screens) | #154 | #108 part 2: codes at checkout, tried with the server before they are kept; vouchers named on the order; `/shop/vouchers` and `/admin/vouchers` to create, list and disable |
| [071](../../specs/071-orchestrator-health/) | Orchestrator health | #155 | #115: `/health` with the saga database and the broker, routed by the gateway; compose, CI and verify-saga wait for it |
| [072](../../specs/072-revenue-paid-day/) | Revenue on the paid day | #156 | #116: `orders.PaidAt` written by the settlement; every insight dates a sale by `PaidAt ?? CreatedAt` |
| [073](../../specs/073-test-debris/) | Test runs clean up | #157 | #118: Bruno's `teardown` folder and the scripts' `trap ... EXIT` delete what each run made; the seller folder's run order made explicit |
| [074](../../specs/074-search-index/) | Search index | #158 | #113: `f_unaccent` + `pg_trgm` GIN indexes, an escaped `LIKE`, translations as a UNION of ids; 452 ms to 1.2 ms on 100,000 products |
| [075](../../specs/075-saved-products/) | Saved products | #159 | #109: a heart on every product, `/saved`, and a `SavedBackInStock` notice once per flip of the rollup; `saved_products` in Catalog |
| [076](../../specs/076-product-questions/) | Product questions | #160 | #110: a customer asks on the product page, only its seller answers (staff for the shop's own, a 404 for anybody else), staff hide a question or only its answer; `product_questions` in Catalog |
| [077](../../specs/077-email-templates/) | Email templates | #161 | #150 (the email half): administrators edit every email in a rich-text editor; append-only versions, allow-list sanitised HTML, placeholders checked by name, preview, a test to yourself; emails sent as HTML plus text |
| [078](../../specs/078-notification-wording/) | Notification wording | #162 | #150 (closes it): administrators reword every notice; versions in Activity, placeholders declared per kind in `notification-kinds.json`, emphasis and links only, notices shown as sanitised HTML with escaped values over the bundled words |
| [079](../../specs/079-object-storage/) | Object storage | #163 | #114: product images in an S3-compatible bucket every Catalog instance shares - SeaweedFS in compose and CI (MinIO no longer publishes images); the old volume imported at startup; verified with two live instances |
| [080](../../specs/080-e2e-browser/) | Browser end to end | #164 | #117: Playwright drives the storefront against the compose stack - a moderator approves, a customer buys and pays, the seller ships, the customer confirms and reviews; Edge locally, Chromium in CI; found and fixed a toast lost whenever saving remounts its form |
| [081](../../specs/081-unlisted-product-reads/) | Unlisted product reads | #169 | #166: a product off the shelf no longer serves its image, reviews or questions to anyone - reviews and questions follow the public lookup's rule, images need their own `ImageAccessKey` |
| [082](../../specs/082-insights-local-days/) | Insights in the shop's days | #170 | #168: every insight counts the shop's days (`Insights:TimeZone`, Asia/Ho_Chi_Minh) - an order paid at 06:30 in Hanoi is that morning's - and the chart draws the days the server counted |
| [083](../../specs/083-more-emails/) | More emails | #171 | #167: eight more emails - parcel shipped, order cancelled, return accepted/refused/refunded, back in stock, account locked/banned - editable like the rest; the reader's language is learnt from use (`users.Language`) |
| [084](../../specs/084-admin-revenue-returns/) | Admin revenue less returns | #176 | #172: a parcel returned and refunded leaves the Overview's revenue, top products and top buyers, as it leaves the seller's page |
| [085](../../specs/085-unlisted-review-writes/) | No reviews off the shelf | #177 | #174: writing a review of a product off the shelf is the same 404 as asking a question there, so no hidden rating moves |
| [086](../../specs/086-product-view-limits/) | Honest view counts | #178 | #173: a product view counts once a day per viewer, and the gateway limits the view endpoint per client |
| [087](../../specs/087-email-delivery/) | Email delivery | #179 | #175: an administrator sees what became of each email - the failed ones with why - and sends a failed one again |
| [088](../../specs/088-relock-limits/) | Re-lock limits | #187 | #180: locking an account again cannot shorten a lock the caller could not have lifted - a moderator no longer undoes an administrator's long lock with a one-day one |
| [089](../../specs/089-explicit-anonymous/) | Signed in by default | #188 | #183: every service refuses an anonymous caller at an endpoint that does not say `[AllowAnonymous]`, and a test in each names any controller action that says nothing |
| [090](../../specs/090-deleted-product-reservations/) | Deleted product's holds | #189 | #181: deleting a product releases its variants' held reservations in the transaction that drops their stock, so no sweeper or settlement acts on a hold whose shelf is gone; settled ones stay as history |
| [091](../../specs/091-back-on-sale-notices/) | Back on sale by any route | #190 | #182: a saved product that can be bought again tells whoever saved it by every route - a variant reactivated, an edit whose rollup flips, a moderator's approval while in stock - in the transaction of the change; the words say "available again" |
| [092](../../specs/092-one-shelf-rule/) | One shelf rule | #191 | #185: "on the shelf" is one property, `Product.OnShelf` (approved and not withdrawn), asked by every public read, shopper write and sale - a withdrawn product is the same 404 as a taken-down one |
| [093](../../specs/093-review-default-pending/) | Pending by default | #192 | #184: `products.ReviewStatus` defaults to `'Pending'`, so an earlier image inserting a seller's product after a rollback files it for review instead of on sale; set by SQL, because a model default would drop `Approved` from EF's inserts |
| [094](../../specs/094-untested-promises/) | Untested promises | #201 | #186: fourteen tests for seven behaviours that were fixed or promised but not held - remount toasts, the wording fallback, frozen tax, the saga's relay, concurrent retries and applications - each shown by a mutation; the staff review list gains the paging rule it lacked |
| [095](../../specs/095-suspended-seller/) | A ban closes the shop | #202 | #193: banning a seller takes every product of theirs off the shelf within seconds - announced by Identity, copied onto the products by Catalog - and lifting the ban puts them back; a lock does not |
| [096](../../specs/096-staff-order-search/) | Find any order | #203 | #194: an administrator finds any order - by the start of its id, the customer's email or its status, failed and cancelled included - at `/admin/orders/find` |
| [097](../../specs/097-category-admin/) | Categories page | #204 | #195: administrators create, rename, translate and delete categories at `/admin/categories`; the slug stays fixed; a duplicate slug is a 409, no longer a 500 |
| [098](../../specs/098-delivery-settings/) | Delivery settings | #205 | #196: delivery prices and options are edited at `/admin/delivery` instead of by redeploying (configuration only seeds what is missing), and the shop's one carrier has a name and a tracking page every reference links to |
| [099](../../specs/099-shop-page/) | A shop has a page | #206 | #197: a shop's name on a product leads to its page - the seller's own description and everything the shop has on the shelf; the seller writes the description beside the rename |
| [100](../../specs/100-moderation-history/) | Moderation history | #207 | #198: a moderator locking somebody sees what staff decided about them before, and why - earlier locks, reviews and questions hidden, products taken down, shop applications refused |
| [101](../../specs/101-content-reports/) | Content reports | #208 | #199: shoppers report a review, a question or a product; moderators work through a queue, most reported first, and every reporter is told how it ended |
| [102](../../specs/102-low-stock-notice/) | Low-stock notice | #209 | #200: a seller is told when a sale takes one of their variants below its line - once per crossing, with a line of their own or the shop's default |
| [103](../../specs/103-commission-at-startup/) | Commission rate at startup | #223 | #210: Order without a usable commission rate refuses to start instead of answering the first checkout with a 500 |
| [104](../../specs/104-seller-cancels-part/) | Seller cancels a part | #224 | #211: a seller who cannot fulfil their part cancels it with a reason - the buyer is refunded that part and the rest of the order ships; the last part cancels the order |
| [105](../../specs/105-correct-tracking/) | Correct a tracking reference | #225 | #212: a mistyped tracking reference is corrected while the parcel is on its way, with the buyer told and both references on the record |
| [106](../../specs/106-payout-accounts/) | Payout accounts | #226 | #213: a seller gives one payout account in Identity (masked to them, emailed on every change); Order asks for it over Admin-only gRPC and freezes bank, holder and last four digits on each payout; none is a 409 |
| [107](../../specs/107-shop-closure/) | Shop pause and closure | #227 | #214: a seller pauses their shop and staff close one with a reason, without touching the account; one shelf statement recomputes from ban, pause and closure |
| [108](../../specs/108-seller-returns/) | A seller's returns | #228 | #215: a seller lists the returns of their own parcels by state at `/shop/returns`, and each sale with one is badged |
| [109](../../specs/109-catalogue-filters/) | Catalogue filters | #229 | #216: the catalogue filters by a range of the "from" price in the currency being browsed in, and by what is in stock - both indexed, both in the address |
| [110](../../specs/110-staff-two-factor/) | Staff two-factor sign-in | #230 | #218: staff sign in with a code from an authenticator app (TOTP); staff roles only in a verified session, so every service refuses an unverified one; recovery codes and an administrator's reset |
| [111](../../specs/111-my-data-export/) | Download my data | #231 | #217 (part 1): a person downloads one file of everything the six services hold about them, with what is withheld and why; every table of every model declared exported, withheld or not personal, and a test per service holds the list to the model |
| [112](../../specs/112-account-deletion/) | Delete my account | #232 | #217 (part 2, closes it): a person deletes their account with their password - refused for staff and while business is open (asked of Order live, its first gRPC service); Identity empties the row and deletes the rest, and each service erases or anonymises what it holds on `AccountDeleted`, keeping the books without a name, email, phone or address |
| [113](../../specs/113-voucher-editing/) | Correcting a voucher | #233 | #219: a seller or administrator corrects an active voucher's name, end, limits and minimums - never what it takes off; the limit guarded against the uses in the statement a checkout's claim serialises against; audited before and after |
| [114](../../specs/114-public-vouchers/) | Public vouchers | #234 | #220: an owner shows a voucher to shoppers or keeps it code only; the live public ones, priced in the shopper's currency and never with a count, are listed on the shop page, product pages and at checkout, where "Use" tries the code |
| [115](../../specs/115-email-failure-alert/) | Email failure alert | #235 | #222: administrators get one in-app digest an hour when emails fail for good, each failure counted once (also by two sweeps at once) and again after a retry; the Overview shows the failed count |
| [116](../../specs/116-activity-retention/) | Activity retention | #236 | #221: read notices go 90 days after reading, unread never; the audit log is kept for ever unless an operator sets years, and every trim is recorded in it; batched, safe on several instances, settings checked at start |
| [117](../../specs/117-seller-sidebar-overflow/) | Seller sidebar | #256 | #238: the seller sidebar keeps to its 15rem column whatever the shop is called - its grids' one column may shrink, so a long name truncates instead of covering the page; the browser flows' seller has a long name and asserts it |
| [118](../../specs/118-order-lines-narrow/) | Order lines at any width | #257 | #239: order lines are two-column rows - details with quantity × price under them, the total never wrapping - so no amount is clipped at checkout or on a phone; the total's rule is one row |
| [119](../../specs/119-cart-after-payment/) | Cart after payment | #258 | #242: the order page re-reads the cart when the order it watched settles (and once more 2 s later, for Cart's consumer), so the header stops counting what was just paid for |
| [120](../../specs/120-not-found-page/) | Not-found page | #259 | #243: an unknown address shows a translated page - what happened, a search box and the way back to the shop - instead of the English words Not found. |
| [121](../../specs/121-staff-labels-status/) | Audit labels and status | #260 | #244: all 112 recorded audit actions have words in both languages, held to the server's source by a test that reads every RecordAsync call; the status page asks all eight health routes by name, held to the gateway's configuration |
| [122](../../specs/122-catalogue-phone/) | Catalogue on a phone | #261 | #245: on a phone the catalogue is two to a row, the hero compact without the featured photo and the categories one scrolling row, so the first product is 807px down instead of 1,355px and the page a third as tall |
| [123](../../specs/123-deleted-accounts-staff/) | Deleted accounts for staff | #262 | #241: staff see a deleted account as Deleted with its date, listed only when asked, and every moderation command on it is 409 AccountDeleted |
| [124](../../specs/124-seller-edits-listing/) | A seller edits a listing | #263 | #240: a seller corrects the original name, description and category (PUT /api/products/{id}, back to review when approved), writes each language's own text and adds variants from the product's page, which now reads signed in |
| [125](../../specs/125-photo-frames/) | Photographs at their own shape | #264 | #251: large product photographs are drawn at their own shape, capped in height, instead of in a square frame between bands of white; the flows upload a 3:2 photograph and measure it |
| [126](../../specs/126-signed-out-add-to-cart/) | Add to cart signed out | #265 | #252: a signed-out shopper sees Add to cart; it opens sign-in saying why and comes back to the same variant; a SKU is shown only once a variant is chosen (no preselection - specs/020 D10 kept) |
| [127](../../specs/127-account-menu/) | One account menu | #266 | #254: one ordered list of account destinations drives the avatar menu, the phone menu and the account page, which each missed something before; account pages start at the same edge as the rest |
| [128](../../specs/128-notice-wording-layout/) | Notice wording layout | #267 | #250: notice wording is edited like the emails - kinds by readable name, a language switch, one kind's sentences, the choice in the address - 1,072px instead of 7,028px |
| [129](../../specs/129-admin-menu/) | Admin menu | #269 | #246: the admin sidebar is grouped, counts what waits in five queues (by role, under their own query keys), and an order keeps the list it was opened from lit and leads back to it |
| [130](../../specs/130-staff-count-keys/) | Staff count keys | #270 | #268: the moderation dashboard and the overview read the sidebar's counts, list keys carry their page size, and a decision re-reads the counts - a one-row count page no longer stands in for a queue's first page |
| [131](../../specs/131-seller-needs-you/) | Seller needs-you | #271 | #247: a seller's home opens with what needs them - sales to prepare (a new status filter on their sales, counted across every page), questions, returns and a missing payout account - and the seller menu badges the same counts |
| [132](../../specs/132-recognisable-orders/) | Recognisable orders | #272 | #248: every order list names up to three lines (the biggest first), and lists and order pages share one short reference - the notices' eight characters, with copy - and a coloured status chip |
| [133](../../specs/133-staff-list-filters/) | Long staff lists can be searched and filtered | #273 | #249: vouchers by code, name and state; people by role and state; categories searched and paged; order tabs counted |
| [134](../../specs/134-checkout-payment-delivery-time/) | Checkout says how payment works and how long delivery takes | #274 | #253: a delivery time per option, set at /admin/delivery; a payment card that says when no money moves |
| [135](../../specs/135-client-workspaces/) | The client becomes a workspace of apps and packages | #281 | #275: npm workspaces - apps/storefront, packages/ui, packages/core; the first step to a back office (ADR-003) |
| [136](../../specs/136-back-office-app/) | A back office for staff, with its own sign-in | #282 | #276: apps/back-office at portal.*: sign-in with a code, a staff guard, its own session and image (ADR-003 step 2) |
| [137](../../specs/137-console-to-back-office/) | The admin and moderator console moves to the back office | #283 | #277: 21 console pages and their layout to apps/back-office, 36 shared components to packages/core; /admin/* redirects; closing a shop moves too (ADR-003 step 3) |
| [138](../../specs/138-staff-roles-back-office/) | Staff roles only in a back-office session | #284 | #278: refresh_tokens.Client from Origin; Admin/Moderator only in a verified back-office session; tools sign in as the back office (ADR-003 step 4) |
| [139](../../specs/139-back-office-audience/) | A separate token audience for the back office | #285 | #280: back-office tokens for their own audience; every service drops staff roles from any other token (ADR-003, second line) |
| [140](../../specs/140-back-office-handoff/) | A one-time handoff from the storefront to the back office | #286 | #279: staff cross with a single-use code in the URL fragment, redeemed for a two-factor challenge - never a session; ADR-003 complete |
| [141](../../specs/141-production-https/) | Production over HTTPS | #296 | #287: the production overlay - published sha- images, no published ports but Caddy's, HTTPS for the storefront and back office hosts, two trusted proxy hops, SMTP over STARTTLS with an account |
| [142](../../specs/142-deploy-rollback/) | Deploy and roll back | #297 | #288: a hand-run deploy workflow ships a release's own files over SSH (host key pinned) and runs deploy.sh: registry check, lock, up --wait, smoke checks through Caddy, releases.log, automatic rollback, previous; a CI dry run renders and asserts the production overlay |
| [143](../../specs/143-vnpay-sandbox/) | Pay with VNPay | #298 | #289: VNPay as a redirect gateway behind Payment's seam: a checkout per order, a signed pay link, the IPN deciding once (signature, merchant, amount, guarded claim), a simulator for development and CI, Pay with VNPay and the return page (ADR-004) |
| [145](../../specs/145-transient-retry/) | Transient retry | #300 | #299: every consumer endpoint retries a transient database failure (40001, 40P01, a lost connection) in a fresh transaction before the EF outbox; found by the load test, which lost a paid order's stock confirmation without it |
| [146](../../specs/146-inventory-read-committed/) | Inventory at READ COMMITTED | #302 | #301: Inventory consumes at READ COMMITTED: zero serialization aborts on a popular product's stock row (thousands per run before), the same median and a tighter tail over eight warm runs; the per-stage breakdown puts the remaining ceiling on that one row's lock |
| [144](../../specs/144-load-tests/) | Load tests | #303 | #290: k6 scenarios against the compose stack - the race for the last units, steady checkouts of one product, browsing - each checking its invariants through the API, with a report generated from kept summaries; their first runs found #299 and #301 |
| [147](../../specs/147-resilience/) | Resilience | #305 | #291: fault.sh stops Payment or the broker, restarts the orchestrator or hangs Inventory during steady checkouts: nothing lost under any, no customer error, recovery times measured; the slow drain after a broker outage filed as #304 |
| [148](../../specs/148-metrics/) | Metrics | #307 | #292: every service pushes OpenTelemetry metrics to Prometheus's OTLP receiver, RabbitMQ scraped, Grafana's provisioned E-commerce overview; orders, settle percentiles and outbox backlogs are gauges from committed rows; the broker-fault run found #306 |
| [149](../../specs/149-inbox-redelivery/) | Inbox redelivery | #308 | #306: a message delivered twice at once (as after a broker outage) faulted on the inbox's unique key into an _error queue; the transient policy now retries that one constraint and the inbox drops the duplicate; reproduced on the real inbox |
| [150](../../specs/150-security-scanning/) | Security scanning | #309 | #293: CodeQL (C# and TypeScript) on every pull request, vulnerable NuGet and runtime npm packages fail the build, Dependabot for five ecosystems, OWASP ZAP's passive baseline of both images with every rule decided in .zap/rules.tsv; first round triaged, each gate shown failing on something planted |
| [151](../../specs/151-security-headers/) | Security headers | #325 | #294: both apps send a CSP (scripts self only; inline styles for Sonner and TipTap), nosniff, X-Frame-Options, Referrer-Policy, Permissions-Policy, COOP/COEP/CORP and no nginx version; Caddy adds HSTS; the image check pins the exact policy, every Playwright test fails on a violation, ZAP's header rules are FAIL |
| [152](../../specs/152-thesis-documents/) | Top-down documents | #327 | #295: an architecture overview with one diagram, a deployment guide from a bare server, the evaluation chapter with every number linked to its run, and a demo script through every role |

Specs 023, 024, 025, 026 and 030 were built without a design record. Theirs were written on 2026-09-27, from the
code at each merge and its pull request, when every record was brought to the standard of specs/001.

## What is next

The open work is listed, with priorities, in the [backlog](backlog.md).
