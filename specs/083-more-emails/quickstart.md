# Quickstart: Validating the eight new emails

> Written on 2026-09-27, after the feature merged (#171), from the code at that merge, the pull request and
> docs/features/email.md.

**Feature**: [spec.md](./spec.md) | **Contracts**: [messages.md](./contracts/messages.md), [http-api.md](./contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # includes Mailpit on :8025
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
```

Every email lands in Mailpit (`http://localhost:8025`, API `http://localhost:8025/api/v1/...`). The dispatcher
sweeps every 15 seconds (Bruno's `ship order` waits up to 40).

---

## Scenario 1 - A shipped parcel is emailed with its tracking reference (SC-001)

Run the Bruno collection (see `CLAUDE.md`). `admin-audit/ship order` ships a paid order with tracking
`BRUNO-<orderId>`, then searches Mailpit for that reference and asserts the subject matches
`/is on its way|đang trên đường/`. Recorded at the merge: **267/267 requests, 437/437 tests**.

By hand, after shipping any order:

```bash
curl -fsS "http://localhost:8025/api/v1/search?query=BRUNO-" | jq '.messages[0].Subject'
```

## Scenario 2 - The language is learnt (SC-004)

```bash
EMAIL=lang-$RANDOM@example.com
curl -fsS -X POST http://localhost:5000/api/auth/register -H 'Content-Type: application/json' \
  -H 'Accept-Language: en-US' -d '{"email":"'$EMAIL'","password":"Passw0rd!","firstName":"Lan","lastName":"Test"}' >/dev/null
curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -H 'Accept-Language: tlh' -d '{"email":"'$EMAIL'","password":"Passw0rd!"}' >/dev/null
```

```sql
-- ecommerce_identity_db on localhost:5435
SELECT "Email", "Language" FROM users WHERE "Email" = '<EMAIL>';   -- en: en-US counted as en, tlh changed nothing
```

Sign in again with `Accept-Language: vi` and the column reads `vi`.

## Scenario 3 - A lock is emailed in the reader's language (US2)

```bash
curl -fsS -X POST http://localhost:5000/api/users/<userId>/lock -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"days":1,"reason":"quickstart 083"}'
```

**Expected**: Mailpit receives one email to that address, in their `users.Language`, giving the end time in UTC.
A lock the moderation rules refuse (for example of an administrator) sends nothing.

```sql
SELECT "Template", "Language", "Status" FROM outgoing_emails
WHERE "RecipientId" = '<userId>' ORDER BY "CreatedAt" DESC LIMIT 3;
```

## Scenario 4 - The tests (SC-002, SC-003, SC-005)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~AccountEmailTests"
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Order.Tests \
  --filter "FullyQualifiedName~Shipping_and_cancelling_each_ask_for_one_email_in_the_orders_language|FullyQualifiedName~ReturnTests"
DB_PASSWORD=<your password> SEAWEEDFS_ACCESS_KEY=... SEAWEEDFS_SECRET_KEY=... \
  dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~SavedProductTests"
cd ../client && npm test -- src/pages/admin-emails
```

**Expected** at the merge: Identity 170/170 (7 in `AccountEmailTests`), Order 265/265, Catalog 205/205, client
457/457. The pull request records four mutations, each red: `QueueEmailCommand` ignoring the reader's language (2
`AccountEmailTests`), a renewal not recording the language, shipping without the email (`NotificationTests`), a
missing Vietnamese label (the admin-emails test).

## Scenario 5 - The console lists all 22 (SC-003)

```bash
curl -fsS http://localhost:5000/api/email-templates -H "Authorization: Bearer $ADMIN" | jq 'length'   # 22
```

Open `/admin/emails` in the storefront: the eight new emails are listed with Vietnamese and English labels, and
each previews with its sample data.
