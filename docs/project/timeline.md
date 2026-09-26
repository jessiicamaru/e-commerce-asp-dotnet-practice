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

Specs 023, 024, 025, 026 and 030 were built without a design record. Theirs were written on 2026-09-27, from the
code at each merge and its pull request, when every record was brought to the standard of specs/001.

## What is next

The open work is listed, with priorities, in the [backlog](backlog.md).
