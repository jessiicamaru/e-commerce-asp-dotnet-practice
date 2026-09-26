# Quickstart: Validating notice rewording

> Written on 2026-09-27, after the feature merged (#162), from the code at that merge, the pull request and docs/features/audit-and-notifications.md.

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md) | **Data**: [data-model.md](./data-model.md)

How to prove the feature works. Each scenario names the requirement or success criterion it checks. These scenarios
were written after the merge; the pull request records the automated results (Activity 35/35, storefront 450/450,
Bruno 263/263 requests) and says nothing about running these steps by hand one by one - not recorded.

---

## Prerequisites

```bash
cd server
docker compose up -d                  # includes postgres-activity on 5440, RabbitMQ, Mailpit
dotnet ef database update --project src/Services/Activity/Ecommerce.Activity.Infrastructure/ \
                          --startup-project src/Services/Activity/Ecommerce.Activity.WebApi/
./start-dev.sh                        # or ./start-dev.ps1 - Identity, Activity and the gateway on :5000 at least
```

The migration `20260926112005_AddNotificationWordingVersions` must be applied (`start-dev` applies it too).

Tokens, through the gateway (the seeded administrator from `ADMIN_EMAIL` / `ADMIN_PASSWORD`, and any account granted
`Moderator` - specs/043):

```bash
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
MOD=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$MOD_EMAIL"'","password":"'"$MOD_PASSWORD"'"}' | jq -r .token)
W=http://localhost:5000/api/notifications/wording
```

---

## Scenario 1 - Nothing edited reads as before (SC-002, US3)

```bash
curl -fsS $W | jq .
```

**Expected**: `200` `{"vi":{},"en":{}}` on a fresh database, and every notice in the bell reads its bundled words.

---

## Scenario 2 - Reword a notice (US1, FR-005, SC-001)

```bash
V=$(curl -fsS $W/all -H "Authorization: Bearer $ADMIN" | jq '[.entries[] | select(.key=="NewSale" and .language=="en") | .version][0] // 0')
curl -fsS -X PUT $W/NewSale/en -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"text":"A <strong>new sale</strong>: {{order}}<script>alert(1)</script>","expectedVersion":'"$V"'}' | jq .
curl -fsS $W | jq .
```

**Expected**: the `PUT` answers `200` with `text` = `A <strong>new sale</strong>: {{order}}` (the script and its text
gone), `isDefault: false`, `version: V + 1`. The public read has `en.NewSale` and no `vi.NewSale`.

Then open the storefront in English as a seller who has a `NewSale` notice: on the next page load the bell shows
**A new sale:** followed by the first eight characters of the order id, with "new sale" in bold. The
headers of the public read say `Cache-Control: public, max-age=60`, so a reload inside that minute may still show the
old words.

---

## Scenario 3 - A placeholder the kind cannot fill is refused by name (US2, FR-007, SC-003)

```bash
curl -sS -X PUT $W/NewSale/en -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"text":"Sold for {{total}}","expectedVersion":'"$((V+1))"'}' | jq '.status, .errors.Text'
```

**Expected**: `400`, and `errors.Text[0]` = `{{total}} is not something a NewSale notice can fill in. It can use: {{order}}.`
Nothing stored: `GET $W/NewSale/en/versions` is unchanged.

---

## Scenario 4 - A link off the shop is refused; a script is stripped (US2, FR-004)

```bash
curl -sS -X PUT $W/OrderPaid/en -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"text":"Paid <a href=\"//evil.test/x\">here</a>","expectedVersion":0}' | jq '.status, .errors.Text'
```

**Expected**: `400` naming `//evil.test/x`. A `javascript:` link is not refused but loses its address (`<a>x</a>`); an
`http(s)://` or `/orders` link is kept.

---

## Scenario 5 - Values are escaped in the page (US2, FR-012, SC-004)

In the storefront's tests:

```bash
cd client
npx vitest run src/utils/notifications src/components/shared/notice-text
```

**Expected**: green, including `escapes the values it fills in` and `keeps emphasis and drops what could run`. To see
the check bite, remove `escapeAll(...)` around the values in `describeNotification` and run again: the escaping test
turns red (the mutation #162 recorded as caught). Restore the file afterwards.

---

## Scenario 6 - Stale, reset, restore, history (US3, FR-008, FR-009, SC-005)

```bash
curl -sS -X PUT $W/NewSale/en -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"text":"Stale","expectedVersion":0}' | jq .status                             # 409
curl -fsS -X POST $W/NewSale/en/reset -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"expectedVersion":'"$((V+1))"'}' | jq '.isDefault, .text'                        # true, null
curl -sS -X POST $W/NewSale/en/reset -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"expectedVersion":'"$((V+2))"'}' | jq .detail                                    # already the storefront's own
curl -fsS -X POST $W/NewSale/en/versions/$((V+1))/restore -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"expectedVersion":'"$((V+2))"'}' | jq '.version, .text'
curl -fsS $W/NewSale/en/versions -H "Authorization: Bearer $ADMIN" | jq '[.[].version]'
```

**Expected**: the stale save is `409` and stores nothing; after the reset `GET $W` has no `en.NewSale`; the second
reset is `409` (its detail is shown in Development only); the restore is version `V + 3` with the version `V + 1`
words; the history reads newest first. Finish with a reset so the shop's words are left as they were.

---

## Scenario 7 - Every change is audited (FR-010, SC-006)

```bash
curl -fsS "http://localhost:5000/api/audit?category=System&subjectType=NotificationWording" \
  -H "Authorization: Bearer $ADMIN" | jq '.items[] | {action, subjectId, summary}'
```

**Expected**: one entry per stored version from scenarios 2 and 6 - `NotificationWordingSaved`,
`NotificationWordingReset`, `NotificationWordingRestored` - with subject `NewSale/en`, and none for the refused saves.
The audit entry arrives through the outbox and the broker, so allow a moment.

---

## Scenario 8 - Administrators only (US4, FR-011, SC-007)

```bash
curl -sS -o /dev/null -w '%{http_code}\n' -X PUT $W/NewSale/en -H "Authorization: Bearer $MOD" \
  -H 'Content-Type: application/json' -d '{"text":"Sold","expectedVersion":0}'           # 403
curl -sS -o /dev/null -w '%{http_code}\n' $W/all -H "Authorization: Bearer $MOD"          # 403
curl -sS -o /dev/null -w '%{http_code}\n' $W/all                                          # 401
curl -sS -o /dev/null -w '%{http_code}\n' $W                                              # 200
```

---

## Scenario 9 - Activity down leaves the bundle (FR-014)

This one is true by construction rather than by a test: the bundle is in i18n from the first render,
`useNotificationWording` asks with `retry: false`, and `applyWording` runs only on an answer. No automated test makes
the fetch fail - stated here rather than implied. By hand: stop Activity, reload any page, and the storefront renders
with its bundled words and no error from the wording query. (The bell's own list comes from Activity too, so with it
down there are no notices to show; the part that can be seen is that nothing else breaks.)

---

## Automated checks

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Activity.Tests --filter "FullyQualifiedName~NotificationWordingTests"
```

**Expected**: 7 passed - storefront view per language, a placeholder refused by name, plural keys and 404s,
sanitising and links, a stale 409 and one number given once (the real `ON CONFLICT` on PostgreSQL 5440), reset and
restore audited, and the overview's placeholders including `ParcelShipped`'s optional `shop`.

```bash
cd client
npx vitest run src/utils/notifications src/components/shared/notice-text src/services/notification-wording src/pages/admin-wording
```

```bash
cd bruno
export ADMIN_EMAIL=... ADMIN_PASSWORD=...
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

**Expected**: `admin-users/` 24-30 green - the public read, a moderator's 403, the overview (`OrderPaid` may use
`order` and `total`), the refusal naming `{{total}}`, the reword with the script stripped, the new words read back in
English only, and the reset.

## Database check

```sql
-- psql -h localhost -p 5440 -U $DB_USER ecommerce_activity_db
SELECT "Key", "Language", "Version", "IsDefault", "Text", "CreatedAt"
FROM notification_wording_versions ORDER BY "Key", "Language", "Version";
```

**Expected**: consecutive versions per key and language, no gaps, no duplicates; a row with `IsDefault` has no text.
