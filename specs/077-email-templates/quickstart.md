# Quickstart: Validating that an administrator edits the emails

> Written on 2026-09-27, after the feature merged (#161), from the code at that merge, the pull request and docs/features/email.md.

**Feature**: [spec.md](./spec.md) | **Contract**: [contracts/http-api.md](./contracts/http-api.md)

How to show the feature working. Each scenario names the requirement or success criterion it proves. At merge,
scenarios 1-6 ran as Bruno requests (`bruno/admin-users/` 17-23, 256/256 requests through the rebuilt storefront
container), scenario 7 as a live check in Mailpit, and scenario 8 as `dotnet test` and `npm test`.

---

## Prerequisites

```bash
cd server
docker compose up -d                      # Identity's Postgres on 5435, RabbitMQ, Mailpit (:8025, SMTP :1025)
dotnet ef database update --project src/Services/Identity/Ecommerce.Identity.Infrastructure/ \
                          --startup-project src/Services/Identity/Ecommerce.Identity.WebApi/
./start-dev.sh                            # or run everything in containers with docker-compose.app.yml
```

Tokens, through the gateway (the login response's `token`):

```bash
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
# MOD: a moderator's token - grant Moderator to a customer at /admin/users, then sign in as them.
T=http://localhost:5000/api/email-templates
```

---

## Scenario 1 - Only administrators (US4, SC-005)

```bash
curl -s -o /dev/null -w '%{http_code}\n' "$T"                                   # 401
curl -s -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer $MOD" "$T"   # 403
```

**Expected**: 401, then 403. Bruno: `a moderator cannot edit the emails is 403`.

## Scenario 2 - The list (FR-004, FR-005)

```bash
curl -s -H "Authorization: Bearer $ADMIN" "$T" | jq 'length, (.[] | select(.template=="PasswordReset" and .language=="en") | .required)'
```

**Expected**: `6`, then `["link"]`. Note `version` of `OrderPaid`/`en` for the next steps (`V`; 0 if never
edited).

## Scenario 3 - A placeholder the email cannot fill is refused by name (US1, FR-011)

```bash
curl -s -X PUT "$T/OrderPaid/en" -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"subject":"Paid {discount}","bodyHtml":"<p>Hi {name}</p>","expectedVersion":'"$V"'}' | jq .errors
```

**Expected**: 400; `errors.Subject[0]` names `{discount}` and lists what the email can use. Repeat on
`PasswordReset/en` with a body without `{link}`: 400, "This email must keep {link}".

## Scenario 4 - A preview strips what could run (US2, FR-003, SC-002)

```bash
curl -s -X POST "$T/OrderPaid/en/preview" -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"subject":"Paid: {order}","bodyHtml":"<p>Thanks, <strong>{name}</strong>!<script>alert(1)</script></p><p><a href=\"{link}\">Your order</a></p>"}' | jq .
```

**Expected**: 200; `subject` starts `Paid: `, `html` has `<strong>` and no `<script`, `text` is not empty and reads
the link as "Your order (http://.../orders/01a0dd2b-sample)". Nothing saved: scenario 2's `version` is unchanged.

## Scenario 5 - Save, then a stale save is 409 (US1, US3, FR-001, SC-003)

```bash
curl -s -X PUT "$T/OrderPaid/en" -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"subject":"Thank you - order {order} is paid","bodyHtml":"<p>Hi {name},</p><p>Order {order} is paid: <strong>{total}</strong>.</p><p><a href=\"{link}\">See your order</a></p>","expectedVersion":'"$V"'}' | jq '.version, .isDefault'
curl -s -o /dev/null -w '%{http_code}\n' -X PUT "$T/OrderPaid/en" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"subject":"Stale","bodyHtml":"<p>{name}</p>","expectedVersion":'"$V"'}'
```

**Expected**: `V+1`, `false`; then 409.

## Scenario 6 - Reset, restore and the audit (US3, FR-008, FR-009, SC-004)

```bash
curl -s -X POST "$T/OrderPaid/en/reset" -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"expectedVersion":'"$((V+1))"'}' | jq '.isDefault, .subject'          # true, "Your order {order} is paid"
curl -s -X POST "$T/OrderPaid/en/versions/$((V+1))/restore" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"expectedVersion":'"$((V+2))"'}' | jq '.version, .isDefault'   # V+3, false
curl -s -H "Authorization: Bearer $ADMIN" "$T/OrderPaid/en/versions" | jq '[.[].version]'
curl -s -H "Authorization: Bearer $ADMIN" "http://localhost:5000/api/audit?subjectId=OrderPaid/en" | jq '.items[].action'
```

**Expected**: the history lists every number, newest first, none rewritten; the audit has
`EmailTemplateRestored`, `EmailTemplateReset`, `EmailTemplateSaved`. Reset once more to leave the shop's emails as
they were (a second reset in a row is 409). In SQL:

```sql
-- psql -h localhost -p 5435 -U $DB_USER ecommerce_identity_db
SELECT "Version", "IsDefault", "Subject", "CreatedBy" FROM email_template_versions
WHERE "Template" = 'OrderPaid' AND "Language" = 'en' ORDER BY "Version";
```

## Scenario 7 - The next email says the saved words, as HTML plus text (US1, FR-006, SC-001)

1. Save new English words for `EmailConfirmation` (keep `{link}`).
2. Register a new account with `Accept-Language: en`.
3. Open Mailpit at http://localhost:8025.

**Expected**: the email's subject is the saved one; the HTML tab shows the saved layout with the token link; the
Text tab shows the text alternative, the link as "words (http://localhost:8088/confirm-email?token=...)". A
Vietnamese registration still gets the Vietnamese built-in words. At merge this was run with the subject "Welcome,
Linh - confirm your address", and the template was reset afterwards. Then **Send me a test** from `/admin/emails`:
one `[Test] ...` email reaches the administrator's own address; with Mailpit stopped the page shows the 503.

## Scenario 8 - The tests

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~EmailTemplateTests"
cd ../client
npm test -- src/pages/admin-emails src/components/shared/rich-text-editor src/services/email-template
```

**Expected**: 13 passing in `EmailTemplateTests` (163/163 for the project at merge); the storefront's tests
passing (435/435 at merge).

## Scenario 9 - No shopper downloads the editor (FR-012, SC-006)

```bash
cd client && npm run build
```

**Expected**: the TipTap code is in its own chunk, not in the main bundle (at merge: main 1.09 MB, the editor chunk
401 kB). Loading the storefront's home page does not request it; opening `/admin/emails` does.
