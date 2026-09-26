# Quickstart: Validating Shop applications

> Written on 2026-09-27, after the feature merged (#96), from the code at that merge, the pull request and
> docs/features/marketplace.md.

**Feature**: [spec.md](./spec.md) | **Contracts**: [http-api.md](./contracts/http-api.md), [messages.md](./contracts/messages.md)

How to prove the feature works. Each scenario names the requirement or success criterion it stands for.
What was actually run at the merge is under [Recorded results](#recorded-results); the commands here
were not re-run when this file was written.

⚠️ These describe the feature **as merged**. Since specs/063 an unconfirmed address is refused the
application (403 `EmailNotConfirmed`) and approving a `register-seller` application is 409 until the
applicant confirms - against a current stack, confirm the address first (the link is in Mailpit,
`:8025`), or scenarios 3 and 4 will stop there.

---

## Prerequisites

```bash
cd server
docker compose up -d                  # Postgres for every service, RabbitMQ
./start-dev.sh                        # or start-dev.ps1: every service and the gateway on :5000
```

Identity (5056), Catalog (5057) and Activity (5063) must be running behind the gateway for every scenario
to show everything; Identity alone is enough for scenarios 1 to 5. Identity's database is on host port
5435 (`ecommerce_identity_db`).

```bash
BASE=http://localhost:5000
ADMIN=$(curl -fsS -X POST $BASE/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
```

Set `ADMIN_EMAIL` and `ADMIN_PASSWORD` on lines of their own first - a prefix assignment on the same line
expands to an empty string (see CLAUDE.md's Bruno note).

---

## Scenario 1 - Registering to sell opens no shop (US1, FR-005, SC-003)

```bash
EMAIL="applicant-$(date +%s)@local.test"
REG=$(curl -fsS -X POST $BASE/api/auth/register-seller -H 'Content-Type: application/json' \
  -d '{"email":"'"$EMAIL"'","password":"Passw0rd!23","firstName":"Mai","lastName":"Tran",
       "shopName":"Mai Lens","description":"Mirrorless cameras","phone":"0912 345 678"}')
echo "$REG" | jq '.roles'                               # ["Customer"]
APPLICANT=$(echo "$REG" | jq -r .token)
curl -fsS $BASE/api/shop-applications/mine -H "Authorization: Bearer $APPLICANT" | jq '.[0] | {status, shopName, applicantEmail}'
curl -s -o /dev/null -w '%{http_code}\n' $BASE/api/sellers/me -H "Authorization: Bearer $APPLICANT"
```

**Expected**: roles `["Customer"]`; one application, `"Pending"`, `"Mai Lens"`, `applicantEmail: null`
(own view, FR-003); `/api/sellers/me` is `403`.

```sql
-- psql -h localhost -p 5435 -U $DB_USER ecommerce_identity_db
SELECT count(*) FROM seller_profiles sp JOIN users u ON u."Id" = sp."UserId" WHERE u."Email" = '<EMAIL>';  -- 0
```

---

## Scenario 2 - One pending application per person (FR-001, FR-007)

```bash
curl -s -w '\n%{http_code}\n' -X POST $BASE/api/shop-applications -H "Authorization: Bearer $APPLICANT" \
  -H 'Content-Type: application/json' -d '{"shopName":"Mai Lens 2"}'
curl -s -w '\n%{http_code}\n' -X POST $BASE/api/shop-applications -H 'Content-Type: application/json' \
  -d '{"shopName":"Nobody"}'
```

**Expected**: `409` `An application is already waiting for review.`; then `401` without a token.

```sql
SELECT indexdef FROM pg_indexes WHERE indexname = 'IX_shop_applications_one_pending';
-- CREATE UNIQUE INDEX ... ON public.shop_applications USING btree ("UserId") WHERE (("Status")::text = 'Pending'::text)
```

---

## Scenario 3 - Staff see the queue, oldest first, with who applied (FR-003, FR-008, FR-013)

```bash
curl -fsS "$BASE/api/shop-applications?status=Pending&pageSize=50" -H "Authorization: Bearer $ADMIN" \
  | jq '.items[] | {shopName, applicantEmail, applicantName, createdAt}'
curl -s -o /dev/null -w '%{http_code}\n' "$BASE/api/shop-applications?status=Pending" -H "Authorization: Bearer $APPLICANT"
```

**Expected**: the application appears with the applicant's email and name, pending ones in ascending
`createdAt`; the applicant asking is `403`.

---

## Scenario 4 - Approval does everything, once (US2, FR-002, FR-009, SC-001, SC-004)

```bash
APP=$(curl -fsS $BASE/api/shop-applications/mine -H "Authorization: Bearer $APPLICANT" | jq -r '.[0].id')
curl -s -w '\n%{http_code}\n' -X POST $BASE/api/shop-applications/$APP/approve -H "Authorization: Bearer $APPLICANT"   # 403
curl -fsS -X POST $BASE/api/shop-applications/$APP/approve -H "Authorization: Bearer $ADMIN" | jq '{status, decidedAt}'
curl -s -w '\n%{http_code}\n' -X POST $BASE/api/shop-applications/$APP/approve -H "Authorization: Bearer $ADMIN"       # 409
curl -fsS -X POST $BASE/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$EMAIL"'","password":"Passw0rd!23"}' | jq '.roles'
```

**Expected**: `403` for a customer; `"Approved"` with a `decidedAt`; `409` `This application is already
approved.`; the new sign-in holds `Seller` and `Customer`.

```sql
SELECT "Status", "DecidedBy" IS NOT NULL, "DecidedAt" IS NOT NULL FROM shop_applications WHERE "Id" = '<APP>';   -- Approved, t, t
SELECT count(*) FROM seller_profiles WHERE "UserId" = '<applicant id>';                                          -- 1
-- Catalog, port 5433, ecommerce_catalog_db, a few seconds later:
SELECT "ShopName" FROM sellers WHERE "SellerId" = '<applicant id>';                                              -- Mai Lens
```

In Activity: one `ShopApproved` audit entry under Moderation (`GET $BASE/api/audit` as admin), and the
applicant's bell shows "Your shop “Mai Lens” is approved."

---

## Scenario 5 - Reject with a reason, then apply again (FR-010, SC-005)

With a second applicant (`APPLICANT2`, registered as in scenario 1) and their application id `APP2`:

```bash
curl -s -w '\n%{http_code}\n' -X POST $BASE/api/shop-applications/$APP2/reject -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"reason":" "}'                                            # 400
curl -fsS -X POST $BASE/api/shop-applications/$APP2/reject -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"reason":"Tell us what you sell"}' | jq '{status, decisionReason}'
curl -fsS -X POST $BASE/api/shop-applications -H "Authorization: Bearer $APPLICANT2" \
  -H 'Content-Type: application/json' -d '{"shopName":"Mai Lens","description":"Used Fujifilm bodies"}' | jq .status
curl -fsS $BASE/api/shop-applications/mine -H "Authorization: Bearer $APPLICANT2" | jq 'map(.status)'
```

**Expected**: `400` for a blank reason; `"Rejected"` with the reason; the new application `"Pending"`;
the history `["Pending", "Rejected"]` (newest first).

---

## Scenario 6 - Five approvals at once open one shop (SC-002)

Automated, against the real Identity database on 5435:

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~ShopApplicationTests"
```

**Expected**: 6 tests pass, among them `Two_simultaneous_approvals_open_one_shop` (five approvals, exactly
one succeeds, one shop). The rest: `Registering_to_sell_opens_no_shop_and_tells_Catalog_nothing`,
`Approval_makes_a_seller_opens_the_shop_and_tells_Catalog_and_the_applicant_once`,
`A_rejection_says_why_and_the_applicant_may_apply_again`,
`A_customer_applies_from_their_account_and_a_seller_cannot_apply_again`,
`Staff_see_the_queue_oldest_first_with_who_applied`. `SellerRolesTests` and `AuditTests` exercise sellers
through an approval too (`IdentityTestFixture.ApprovedSellerAsync`).

A database is required: the guarantees under test are the guarded update and the partial unique index.

---

## Scenario 7 - The Bruno round trip (SC-001)

```bash
cd bruno
export ADMIN_EMAIL=... ADMIN_PASSWORD=...
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

The `seller/` folder at the merge begins: `register a seller` (roles `["Customer"]`), `the applicant waits
for review`, `an applicant has no shop yet` (403), `a customer cannot approve a shop` (403), `staff see
the application in the queue`, `an administrator approves the shop`, `approving it again is 409`, `the
seller signs in again as a seller`, then the rest of the folder, including `the shop name reaches the
catalogue`. `security-checks/applying to sell without a token is 401`.

---

## Scenario 8 - The storefront (US3)

```bash
cd client && npm test -- open-shop admin-shops admin-layout
```

By hand: sign in as a customer, open the user menu - "Open a shop" - and send an application; the form
disappears and "Waiting for review" shows. As a moderator, `/admin` opens on `/admin/shops`; approve it.
Back as the applicant, without signing out, press "Go to my shop" on `/open-shop`: the session is renewed
and `/shop` opens.

---

## Recorded results

From PR #96, at the merge:

- Identity tests **71/71**, including the 6 new `ShopApplicationTests`.
- Client **205/205**; lint, type-check and build clean.
- Bruno **156/156 requests, 249 tests**.
- `verify-saga.sh` passes.
- Mutation checks, each red and then restored: removing the `Status = 'Pending'` guard fails the
  approval test and the race test; showing the form while an application is waiting fails the page test.
- Screenshots of the queue at 1360px, and at 390px with no horizontal overflow.

Whether scenarios 1 to 5 and 8 were run by hand with curl and SQL as written here: not recorded. The
Bruno run covers the same ground as 1 to 4.
