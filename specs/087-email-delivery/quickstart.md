# Quickstart: Validating email delivery

> Written on 2026-09-27, after the feature merged (#179), from the code at that merge, the pull request and
> docs/features/email.md.

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # includes Mailpit on :8025
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
```

---

## Scenario 1 - The lists, never the data (SC-001)

```bash
curl -fsS "http://localhost:5000/api/emails" -H "Authorization: Bearer $ADMIN" | jq '.totalCount'
curl -fsS "http://localhost:5000/api/emails?status=Sent&pageSize=5" -H "Authorization: Bearer $ADMIN" \
  | jq '.items[] | keys'          # no "data", no "dataJson"
curl -fsS "http://localhost:5000/api/emails?status=Sent&search=lan%40" -H "Authorization: Bearer $ADMIN" | jq '.items | length'
```

## Scenario 2 - Make an email fail, then send it again (SC-002)

Stop Mailpit (`docker stop e-commerce-mailpit`), trigger an email (for example pay for an order), and wait until it
has failed. Twelve attempts back off to an hour (specs/060), so for a quick check mark one failed by hand instead:

```sql
-- ecommerce_identity_db on localhost:5435
UPDATE outgoing_emails SET "Status" = 'Failed', "LastError" = 'quickstart 087'
WHERE "Id" = (SELECT "Id" FROM outgoing_emails WHERE "Template" = 'OrderPaid' ORDER BY "CreatedAt" DESC LIMIT 1)
RETURNING "Id";
```

Then, with Mailpit running:

```bash
curl -fsS -X POST http://localhost:5000/api/emails/<id>/retry -H "Authorization: Bearer $ADMIN" | jq '{status, attempts, canRetry}'
# {"status":"Pending","attempts":0,"canRetry":false}
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5000/api/emails/<id>/retry -H "Authorization: Bearer $ADMIN"   # 409
```

**Expected**: within a dispatcher sweep the row reads `Sent` and Mailpit holds the email once more; the audit log
(`GET /api/audit`, Admin) has an `EmailRetried` entry whose "before" carries the old attempts and `quickstart 087`.

## Scenario 3 - A reset link is never retried (SC-003)

```sql
UPDATE outgoing_emails SET "Status" = 'Failed'
WHERE "Id" = (SELECT "Id" FROM outgoing_emails WHERE "Template" = 'PasswordReset' ORDER BY "CreatedAt" DESC LIMIT 1)
RETURNING "Id";
```

```bash
curl -fsS "http://localhost:5000/api/emails" -H "Authorization: Bearer $ADMIN" | jq '.items[] | select(.template=="PasswordReset") | .canRetry'   # false
curl -s -X POST http://localhost:5000/api/emails/<id>/retry -H "Authorization: Bearer $ADMIN" | jq .detail
# "A reset or confirmation link expires; the person asks for a new one instead."
```

## Scenario 4 - The tests (SC-001 to SC-003, SC-005)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~EmailDeliveryTests"
cd ../client && npm test -- src/services/outgoing-email src/pages/admin-email-delivery
```

**Expected** at the merge: Identity 174/174 (4 in `EmailDeliveryTests`), client 466/466. The pull request records
three mutations, each red: the retry without its guard; a reset link allowed to be retried; the data returned.

## Scenario 5 - Bruno (SC-001, SC-004)

Run the collection (see `CLAUDE.md`): `admin-users/` seq 31 (moderator 403), 32 (the sent emails, no data, none
retryable), 33 (a sent email is not sent again, 409) and `security-checks/the email log without a token is 401`.
Recorded at the merge: **272/272 requests, 444/444 tests**.

## Scenario 6 - The page (FR-004)

Open `/admin/email-delivery` as an administrator: it opens on the failed emails with who and why; the tabs switch
state, the search narrows to one person, "Send again" appears only where `canRetry`, and a refusal is shown in the
server's words. A moderator does not see the entry in the admin menu.
