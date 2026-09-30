# Personal data

What the shop holds about a person, and how they get a copy of it. Part of
[#217](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/217): the download is
[specs/111](../../specs/111-my-data-export/); deleting an account is specs/112.

## What a person can do

On **Your account** (`/account`) a signed-in person presses **Download my data** and the browser saves one file,
`my-data-<date>.json`. It holds everything the shop keeps about them, service by service, and lists what was kept back
with the reason. Nothing is prepared in the background and nothing is kept: the file is composed in the browser from
six answers, so it is as current as the moment the button was pressed.

If a service does not answer, the file is still saved. That service appears as `{ "unavailable": true }` and a notice
names the missing part ("without payments") - a file that silently lacked the orders would read as "the shop holds no
orders about me", which is untrue.

## How it works

Each service answers for its own database, from the token - there is no person id in any address, so nobody can ask
about anybody else:

| Service | Address | Sections |
| :-- | :-- | :-- |
| Identity | `GET /api/auth/me/data` | `profile`, `addresses`, `shop`, `payoutAccount`, `shopApplications`, `emails` |
| Catalog | `GET /api/products/my-data` | `reviews`, `questions`, `savedProducts`, `reports`, `reviewEligibility`, `shop`, `products` |
| Order | `GET /api/orders/my-data` | `orders` (lines, frozen address, parcels), `returns`, `voucherUses`, `voucherUseCounts`, `vouchers`, `payouts` |
| Cart | `GET /api/cart/my-data` | `cart` |
| Payment | `GET /api/payments/my-data` | `payments`, `refunds` |
| Activity | `GET /api/notifications/my-data` | `notifications`, `activity` |

Every answer has the same shape - `{ service, exportedAt, sections, withheld }` - and every section is an array, even
when it holds one row. The storefront wraps the six as `{ exportedAt, person: { id, email }, services }`.

Why the storefront composes the file rather than one service collecting it: the same reason the Overview is composed
by the client (specs/047) - no new synchronous edge between services and no message round trip, for something a
person asks for now and then. The cost is that a service being down makes a partial file, which the file says.

## The inventory - every table declared

Each service declares **every table of its model** in one place, `<Service>PersonalData.Inventory`
(`Ecommerce.Shared.PersonalData.PersonalDataInventory`): **exported** (into which section), **withheld** (with a
reason the person reads), or **not personal**. The broker's outbox tables are excluded automatically.

⚠️ `MyDataTests.Every_table_of_the_model_is_declared_...` in each service compares the declaration with the EF model.
**A new table fails that test until it is declared** - which is the point: a table holding a person's data cannot be
added and quietly left out of their export, or out of the deletion (specs/112), which reads the same list.

What is withheld, and why:

| Service | Table | Reason |
| :-- | :-- | :-- |
| Identity | `refresh_tokens`, `password_reset_tokens`, `email_confirmation_tokens`, `two_factor_challenges`, `two_factor_recovery_codes` | Secrets (stored as hashes) that would sign somebody in; a copy in a downloaded file is a copy to lose |
| Identity | `sign_in_throttles` | Keyed by email to slow down guessing; counts of failed attempts, not the person's data |
| Catalog | `product_viewers` | A hash of a session or browser id that counts a view once; it cannot be traced back to the person |
| Cart | `checkout_outcomes` | Working records that match a checkout to its outcome; the order itself is in Order's export |

Inventory holds no person's id, and the orchestrator's saga state holds the buyer's id only while an order settles
(working state, no HTTP) - neither has an export.

## Rules, and why

- **Projections, never entities.** Each reader selects fields by name, so a new column is not exported until somebody
  decides it should be. The password hash, the TOTP secret, an email's data (which may hold a link) and a payout
  account's full number (masked, as on the seller's page) are in none of them; `MyDataTests` plants each and asserts
  it is absent.
- **The person's data, not other people's.** Order exports the orders a person *placed*, never a seller's sales -
  each sale is somebody else's order. Staff who hid a review, resolved a report or recorded a payout are not named.
  The audit log gives the entries a person made or that were decided about them (`AboutUserId`, specs/100), without
  the actor's identity and without the before/after snapshots, which can hold somebody else's data.
- **A refund names nobody** - it is the person's through the payment it gives back.
- **Payment is still a stub**: every payment and refund says `provider: "Stub"`, and the export keeps saying it.

## Tests

`MyDataTests` in Identity, Catalog, Order, Cart, Payment and Activity: the inventory matches the model; two people,
each export holds only its caller's rows in every section; the secrets and other people's data are absent. Identity's
also holds `PersonalDataInventory.Problems` to each way a declaration can be wrong. The storefront's account page tests
ask all six, save one file, and mark and name a service that failed. Bruno's `my-data` folder asks each service as the
customer, and `security-checks` holds the six 401s.

## History

- specs/111 (#217, part 1): the download.
