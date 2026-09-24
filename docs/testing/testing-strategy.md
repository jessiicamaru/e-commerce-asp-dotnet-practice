# Testing strategy

How the system is tested, what each layer can and cannot catch, and the numbers as of 2026-09-24. The
principle behind all of it is the constitution's **evidence over assumption**: a claim that something
works is a test that fails when it does not.

## The layers

| Layer | Tool | Where | How many | Catches |
| :-- | :-- | :-- | :-- | :-- |
| Service integration tests | xUnit, real PostgreSQL | `server/tests/Ecommerce.<Service>.Tests/` | 555 test cases in 8 projects | Business rules, locking, guarded updates, unique constraints, idempotence, what is published |
| Storefront unit tests | Vitest, jsdom, Testing Library | `client/src/**/*.test.ts(x)` | 291 tests in 52 files | What a page asks for, what a form sends, what a guard lets through, how a server refusal is shown |
| API collection | Bruno CLI | `bruno/` | 190 requests, 307 assertions | Every public endpoint through the gateway, with real tokens, including 401/403/404/409 cases |
| Cross-service end to end | Bash + curl | `.github/scripts/verify-saga.sh` | 1 script, both saga branches | Stock, payment, cart and order agreeing after a real checkout; cancellation restocking and refunding |
| Auth smoke | Bash + curl | `.github/scripts/verify-auth.sh` | 1 script | Anonymous 401, wrong role 403, right role through; order ownership with real signed tokens |
| Mutation checks | by hand, per change | recorded in each PR | 2-4 per feature | That a new test fails when the rule it guards is removed |

## Service integration tests

**They run against a real PostgreSQL, never an in-memory provider.** The guarantees under test are the
database's: row locks (`FOR UPDATE`), guarded single-statement updates (`UPDATE ... WHERE "Status" =
'Pending'`), partial unique indexes, CHECK constraints. An in-memory provider evaluates none of them, so
it would pass code that oversells stock or settles an order twice.

Each project creates a throwaway database per test run on the service's own PostgreSQL port and drops
it afterwards. Run them with the password set:

```bash
cd server
DB_PASSWORD=<your password> dotnet test
```

| Project | Port | Test cases | Test classes |
| :-- | :-- | :-- | :-- |
| `Ecommerce.Identity.Tests` | 5435 | 83 | AddressBook, Audit, AuthError, EmailCase, ForbiddenProblem, JwtStartup, Moderation, RefreshTokenReuse, RegistrationValidation, SellerRoles, Session, ShopApplication, UserReport |
| `Ecommerce.Catalog.Tests` | 5433 | 161 | Audit, Availability, CategoryTranslation, DeleteCategory, DeleteProduct, LanguageNegotiation, OrphanImage, ProductImage, ProductReview, ProductView, RequestCurrency, Review, SellerOwnership, Translation, VariantOwnership, VariantPrice, VariantSellerPricing, Variant |
| `Ecommerce.Cart.Tests` | 5439 | 18 | CheckoutOutcome, Validation, VariantLine |
| `Ecommerce.Order.Tests` | 5434 | 184 | Audit, Cancellation, CheckoutQuote, CheckoutShipping, Delivery, Earnings, Fulfilment, Insights, Notification, OrderCurrency, OrderLanguage, OrderQuery, OrderTotals, Payout, SellerSales, Settlement, Shipment, ShopName, TotalsPersistence, VariantCheckout |
| `Ecommerce.Inventory.Tests` | 5437 | 46 | Announcement, Audit, ForgetProduct, ReserveStock, Restock, SellerStock, Settlement |
| `Ecommerce.Payment.Tests` | 5438 | 20 | Audit, ChargeOrder, ConcurrentInsertRecovery, Refund |
| `Ecommerce.Orchestrator.Tests` | 5436 | 15 | OrderStateMachine (the saga's transitions through MassTransit's harness - the first tests it has had, specs/053), PaymentTimeout (the sweeper's query, the options) |
| `Ecommerce.Activity.Tests` | 5440 | 28 | AuditDiff, AuditLog, AuditTrail, Notification, Redaction |

"Test cases" counts each `[Theory]` row; the number of `[Fact]`/`[Theory]` methods is lower.

### What these tests look for

- **Races, run for real.** Five approvals of one shop application at once open one shop
  (`ShopApplicationTests`); fifty simultaneous payment requests record one payment
  (`ConcurrentInsertRecoveryTests`); two sellers shipping at once leave the order `Shipped`
  (`ShipmentTests`); a cancel and a ship on one order serialise (`CancellationTests`); twenty
  simultaneous product views count twenty (`ProductViewTests`).
- **Redelivery.** A message handled twice changes nothing the second time: settlement, restock, refund,
  audit entries, notifications, review eligibility.
- **Who may do what to whom.** Somebody else's product, sale, notification or review is a 404, not a 403
  (`SellerOwnershipTests`, `SellerSalesTests`, `NotificationTests`).
- **What is published.** The MassTransit test harness records every published message, so a test can
  assert that an approval announced the seller once and notified the applicant once.
- **The shape of what is returned.** A seller's view of a sale has no field for the order total or the
  customer (`SellerSalesTests`), so the leak cannot happen by adding a property.

### Traps these tests have hit

- A shared test caller (`TestCaller`) is a singleton for the whole collection: a test that becomes a
  seller must put it back, or later tests run as that seller.
- A test sending a command outside a MassTransit consumer does not reproduce a consumer's transaction;
  `NotificationTests.Settling_inside_a_consumer_transaction_joins_it` opens one on purpose (PR #94).
- Seeding an old schema with today's EF model fails once the model gains a column; `EmailCaseTests`
  inserts its old-schema rows with SQL.

## Storefront unit tests

`npm test` in `client/`. `src/test/setup.ts` makes any test that reaches the network fail, and each test
pins its language (`i18n.changeLanguage('en')`) so it asserts the same words on every machine. Services
are stubbed with `vi.spyOn(Service, 'method')`; `src/test/render.tsx` renders a page signed in as a
customer, seller, moderator or administrator. The rule is to test the logic that can be wrong - what a
hook asks for, what a form sends, what a guard lets through, how a server refusal is shown - not that a
`div` rendered.

`findBy` and `waitFor` wait up to 3 seconds (raised from 1 in PR #101): with 51 files running in parallel
the first render in a file can take longer than a second.

## Bruno collection

`bruno/` covers every public endpoint through the gateway, in folders by area, with a test on every
request. It runs top to bottom, carrying tokens and ids from one request to the next:

| Folder | Requests | Folder | Requests |
| :-- | :-- | :-- | :-- |
| health-check | 6 | admin-audit | 14 |
| auth | 6 | notifications | 6 |
| category | 4 | admin-users | 15 |
| product | 21 | admin-insights | 9 |
| inventory | 3 | reviews | 10 |
| addresses | 8 | security-checks (401/403/404/409) | 38 |
| cart | 6 | seller (runs last) | 36 |
| order | 8 | | |

```bash
cd bruno
export ADMIN_EMAIL=...          # on its own line: a one-line prefix sends an empty string
export ADMIN_PASSWORD=...
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

Adding or changing an endpoint means adding a Bruno request in the same change.

## End-to-end scripts

`verify-saga.sh` is the only check that sees between services. It places a real order with a real
customer token, follows it to a terminal state, and asserts stock **on hand and reserved** (never the
derived "available" alone - during an earlier bug "available" was right while the units stayed held),
the cart afterwards, and - for a cancelled order - the stock back and one full refund. It needs all
services running and reports "skipped" rather than passing when they are not. Payment decides its
outcome once at startup, so CI runs it twice with Payment restarted in between to cover both branches.

## Mutation checks

When code is written before its tests, or a test might pass for the wrong reason, the rule it guards is
deliberately broken - a guard removed, a status added to a filter - and the test must turn red. The
mutations and their results are recorded in each pull request. One trap: restoring a file with
`mv file.bak file` gives it an older timestamp than the mutated build, and MSBuild then keeps testing the
mutated binary - `touch` the restored file.

## Continuous integration

`.github/workflows/ci.yml` runs on every push and pull request to `main`:

| Job | What it does |
| :-- | :-- |
| Build and test | Builds the solution, runs every service test project against PostgreSQL containers |
| Storefront build | Lints (oxlint), tests, type-checks and builds `client/`; builds the storefront image and runs `verify-storefront-image.sh` against it - the app, a deep link, `/api` forwarded, a 2 MB upload, cache headers (specs/051) |
| Schema compatibility | Comments on a migration that drops, renames or narrows the schema (never blocks) |
| Image carries no secret | Checks every layer of every image for credentials |
| Auth smoke test | `verify-auth.sh` against three services |
| Saga end-to-end | All services and RabbitMQ in containers; `verify-saga.sh` on both branches |
| Publish images | On `main` only, after the checks (the storefront's included): ten images - nine server images and the storefront - with immutable `sha-` tags to GHCR |

A pull request is squash-merged only when every job is green.

## What is not tested yet

- No test drives the storefront in a browser against the running stack -
  [#117](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/117).
- Bruno and `verify-saga.sh` leave the products they create behind -
  [#118](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/118).
- No load or performance tests; search has no index
  ([#113](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/113)).
