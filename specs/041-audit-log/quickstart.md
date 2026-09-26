# Quickstart: Validating the audit log

> Written on 2026-09-27, after the feature merged (#93), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Feature**: [spec.md](spec.md) | **Contracts**: [http-api.md](contracts/http-api.md),
[messages.md](contracts/messages.md)

How to show the feature works. Each scenario names what it proves.

---

## Prerequisites

```bash
cd server
docker compose up -d                  # includes postgres-activity on 5440
dotnet ef database update --project src/Services/Activity/Ecommerce.Activity.Infrastructure/ \
                          --startup-project src/Services/Activity/Ecommerce.Activity.WebApi/
./start-dev.sh                        # every service, Activity on 5063, the gateway on 5000
```

`server/.env` needs `ACTIVITY_DB_PORT=5440` (in `.env.example`). Tokens:

```bash
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
```

---

## Scenario 1 - The service's own tests (FR-002, FR-003, FR-005, FR-008, FR-009, FR-010)

```bash
cd server
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Activity.Tests
```

**Expected**: all pass - 24 at the merge (`AuditDiffTests`, `AuditLogTests`, `AuditTrailTests`,
`RedactionTests`). They cover the diff (including a JSON `null` leaf and the 200-change cut), redaction
at any depth, the actor's role ranking, a redelivered entry kept once, the filters, the summary, 404 for
an unknown entry and 400 for an unknown category.

## Scenario 2 - Each service records one entry per action (SC-001, FR-001)

```bash
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Identity.Tests  --filter FullyQualifiedName~AuditTests
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Catalog.Tests   --filter FullyQualifiedName~AuditTests
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Inventory.Tests --filter FullyQualifiedName~AuditTests
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Order.Tests     --filter FullyQualifiedName~AuditTests
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Payment.Tests   --filter FullyQualifiedName~AuditTests
```

**Expected**: Identity 3, Catalog 2, Inventory 2, Order 4, Payment 1 pass. Each asserts one entry per
action with the right actor, and none for a refused change (`A_refused_change_is_not_recorded`,
`A_refused_step_is_not_recorded`).

## Scenario 3 - An order's life, read back (US1, US2 scenario 1, SC-003)

Place an order, prepare, ship and receive it (the Bruno collection does this), then:

```bash
curl -fsS "http://localhost:5000/api/audit?subjectType=Order&subjectId=$ORDER_ID&pageSize=50" \
  -H "Authorization: Bearer $ADMIN" | jq '[.items[].action]'
```

**Expected**: `OrderPlaced`, `ParcelPrepared`, `ParcelShipped`, `ParcelReceived` - once each - plus the
payment's `PaymentCharged` (subject `Order` too). Entries travel through RabbitMQ, so they appear a moment
after the requests.

Or run the Bruno collection: `bruno/admin-audit/the audit log records the order it followed.yml` asserts
exactly this.

## Scenario 4 - Admin only (FR-004)

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/audit                                  # 401
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/audit -H "Authorization: Bearer $CUSTOMER"   # 403
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/audit -H "Authorization: Bearer $ADMIN"      # 200
```

Bruno: `bruno/security-checks/the audit log without a token is 401.yml`,
`bruno/security-checks/a customer cannot read the audit log is 403.yml`.

## Scenario 5 - The summary and the diff (FR-009, FR-003)

```bash
curl -fsS http://localhost:5000/api/audit/summary -H "Authorization: Bearer $ADMIN"
curl -fsS "http://localhost:5000/api/audit/$ENTRY_ID" -H "Authorization: Bearer $ADMIN" | jq .changes
```

**Expected**: one `{category, count}` per category that has entries; for a `PriceSet` or `VariantUpdated`
entry, `changes` lists only the paths whose value changed.

## Scenario 6 - Nothing secret, nothing twice, in the database (FR-002, FR-005)

```sql
-- psql -h localhost -p 5440 -U $DB_USER ecommerce_activity_db
SELECT "Id", count(*) FROM audit_entries GROUP BY "Id" HAVING count(*) > 1;           -- no rows
SELECT count(*) FROM audit_entries
 WHERE "Before"::text ~* '"[^"]*(password|token|secret|hash)[^"]*":\s*"(?!\*\*\*)'
    OR "After"::text  ~* '"[^"]*(password|token|secret|hash)[^"]*":\s*"(?!\*\*\*)';   -- 0
```

(PostgreSQL regular expressions support the lookahead used here.)

## Scenario 7 - The admin page

Sign in as the administrator and open `/admin/audit` in the storefront. **Expected**: one tab per
category with its count, filters for the actor and the period kept in the address, newest first; opening
an entry shows its changes old → new. At the merge the page had no horizontal overflow at 390px.

## Scenario 8 - The saga still settles (SC-002)

```bash
cd server
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
```

**Expected**: pass. Recording adds an outbox message to each transaction; it must not change what the
saga does.

---

## Results recorded at the merge (#93)

Activity 24 tests; per-service audit tests Identity 3, Catalog 2, Inventory 2, Payment 1, Order 4; every
existing suite still green (Order 168, Catalog 137, Identity 57, Inventory 46, Payment 19, Cart 14);
client 175 tests, lint clean, build passes; Bruno 125/125; `verify-saga.sh` pass. Scenario 6's SQL was
not part of the recorded evidence - it is offered here as a check, not as a result.
