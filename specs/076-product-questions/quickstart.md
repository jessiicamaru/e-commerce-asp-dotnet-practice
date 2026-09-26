# Quickstart: Validating product questions

> Written on 2026-09-27, after the feature merged (#160), from the code at that merge, the pull request and docs/features/product-questions.md.

**Feature**: [spec.md](./spec.md) | **Contracts**: [http-api.md](./contracts/http-api.md), [messages.md](./contracts/messages.md)

How to prove the feature works. Each scenario names the requirement or success criterion it checks. The pull
request records these results at the merge: `Ecommerce.Catalog.Tests` **186/186** (10 new in
`ProductQuestionTests`), the storefront **427/427** in 74 files with lint and `tsc -b` clean, and Bruno **249/249
requests, 403/403 tests** through the rebuilt storefront container. This file was written afterwards and its
commands were **not** re-run for it.

---

## Prerequisites

```bash
cd server
docker compose up -d                  # Postgres for Catalog on 5433, RabbitMQ, Mailpit, SeaweedFS ...
./start-dev.sh                        # or ./start-dev.ps1 - migrations, then every service and the gateway on :5000
```

`AddProductQuestions` is applied with Catalog's other migrations; by hand:

```bash
dotnet ef database update --project src/Services/Catalog/Ecommerce.Catalog.Infrastructure/ \
                          --startup-project src/Services/Catalog/Ecommerce.Catalog.WebApi/
```

Tokens, through the gateway (the auth response carries `token`):

```bash
login() { curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$1"'","password":"'"$2"'"}' | jq -r .token; }
ADMIN=$(login "$ADMIN_EMAIL" "$ADMIN_PASSWORD")
CUSTOMER=$(login customer@example.test '<password>')   # any registered customer
SELLER=$(login seller@example.test '<password>')       # an approved seller who owns $PRODUCT
```

`$PRODUCT` is a seller's product that a moderator has approved (on sale), and `$SHOP_PRODUCT` one the shop lists
itself (`SellerId` null). Bruno's `seller/` folder builds exactly this state before it reaches the questions
requests.

---

## Scenario 1 - The automated suite (every FR, SC-001 to SC-005)

```bash
cd server
DB_PASSWORD=<your password> SEAWEEDFS_ACCESS_KEY=... SEAWEEDFS_SECRET_KEY=... \
  dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~ProductQuestionTests"
```

**Expected**: 10 tests pass, against the real PostgreSQL on 5433 - the property under test in
`Ten_first_answers_at_once_tell_the_asker_once` is the database's row locking on a guarded `UPDATE`
(Principle V). The S3 keys are needed by today's Catalog fixture (specs/079); at the merge they were not.

```bash
cd client && npm test -- product-questions shop-questions admin-questions services/question
```

**Expected**: the four suites named in [tasks.md](./tasks.md) pass.

---

## Scenario 2 - A customer asks; the seller is told (US1, FR-003, FR-004)

```bash
Q=$(curl -fsS -X POST http://localhost:5000/api/products/$PRODUCT/questions \
  -H "Authorization: Bearer $CUSTOMER" -H 'Content-Type: application/json' \
  -d '{"body":"Does it come with the charger?"}' | tee /dev/stderr | jq -r .id)
curl -fsS http://localhost:5000/api/products/$PRODUCT/questions | jq '.items[0]'
```

**Expected**: `200`, `askerName` is the customer's first name (from the token), `answer` null. The public list
shows it first. The seller's bell has `NewQuestion` (`GET /api/notifications` with `$SELLER`). Asking without a token
is `401`; asking about a product that is not approved is `404 Product not found.`.

---

## Scenario 3 - Only the seller answers; an administrator is a 404 (US2, FR-002, SC-001)

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X PUT http://localhost:5000/api/questions/$Q/answer \
  -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' -d '{"answer":"Yes"}'      # 404
curl -s -X PUT http://localhost:5000/api/questions/$(uuidgen)/answer \
  -H "Authorization: Bearer $SELLER" -H 'Content-Type: application/json' -d '{"answer":"Yes"}' | jq .detail
curl -fsS -X PUT http://localhost:5000/api/questions/$Q/answer \
  -H "Authorization: Bearer $SELLER" -H 'Content-Type: application/json' -d '{"answer":"Yes, in the box."}' | jq
```

**Expected**: the administrator gets `404` with the same `Question not found.` a made-up id gets (in Development,
where the detail is shown). The seller gets `200`, full shape, `answerEdited: false`; the customer has
`QuestionAnswered`. Answering again rewrites it: `answerEdited` becomes true a second later, and no second notice
arrives.

---

## Scenario 4 - The seller asking about their own product (US1, FR-003)

```bash
curl -s -X POST http://localhost:5000/api/products/$PRODUCT/questions -H "Authorization: Bearer $SELLER" \
  -H 'Content-Type: application/json' -d '{"body":"Is my own product any good?"}' | jq '.status, .detail'
```

**Expected**: `403`, `You cannot ask about your own product.`

---

## Scenario 5 - Staff answer the shop's own, and queue it (US2)

Ask on `$SHOP_PRODUCT` as the customer, then:

```bash
curl -fsS "http://localhost:5000/api/questions/to-answer?answered=false" -H "Authorization: Bearer $ADMIN" | jq '.items[].productName'
```

**Expected**: the shop's question is there and no seller's question is; nobody was notified when it was asked. The
seller's own `to-answer` lists only their products' questions. A seller answering the shop's question is `404`.

---

## Scenario 6 - Hide the answer, and the rewrite is locked (US3, FR-002)

```bash
curl -fsS -X POST http://localhost:5000/api/questions/$Q/answer/hide -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"reason":"Sends shoppers elsewhere"}' | jq '.answerHiddenAt, .hiddenAt'
curl -fsS http://localhost:5000/api/products/$PRODUCT/questions | jq '.items[0].answer'                        # null
curl -s -o /dev/null -w '%{http_code}\n' -X PUT http://localhost:5000/api/questions/$Q/answer \
  -H "Authorization: Bearer $SELLER" -H 'Content-Type: application/json' -d '{"answer":"Same again"}'          # 409
curl -fsS -X POST http://localhost:5000/api/questions/$Q/answer/restore -H "Authorization: Bearer $ADMIN" >/dev/null
```

**Expected**: the public list reads the question as unanswered; the seller has `AnswerHidden` with the reason; the
rewrite is `409` until the restore, then `200`. Hiding the question instead (`/hide`) takes it off the list, tells
the asker (`QuestionHidden`), and a second hide is `409 This question is already hidden.`

---

## Scenario 7 - A customer cannot read the moderators' list

```bash
curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5000/api/questions?hidden=true" -H "Authorization: Bearer $CUSTOMER"
```

**Expected**: `403`.

---

## Scenario 8 - The audit log and the database

```sql
-- psql -h localhost -p 5433 -U $DB_USER ecommerce_catalog_db
SELECT "Body", "AnsweredAt", "AnswerUpdatedAt", "AnswerHiddenAt", "AnswerHiddenReason"
FROM product_questions WHERE "Id" = '<Q>';
```

**Expected**: `AnsweredAt` is the first answer and did not move on the rewrite; the hidden columns are null again
after the restore. `GET /api/audit?subjectType=Question&subjectId=<Q>` as an administrator shows `QuestionAsked`, `QuestionAnswered`,
`AnswerEdited`, `AnswerHidden` and `AnswerRestored` (FR-007, SC-004).

---

## Scenario 9 - Bruno

```bash
cd bruno
export ADMIN_EMAIL=... ADMIN_PASSWORD=...
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

**Expected**: the whole collection passes; the questions requests are `seller/` seq 67 to 77 (approve the product
again, ask, the public list, the seller's queue, an administrator answering for a seller is 404, the seller answers,
a seller asking about their own product is 403, staff hide the answer, the rewrite is 409, a customer on the staff
list is 403, asking without a token is 401). The `seller/` folder needs Mailpit up (specs/063).

---

## Scenario 10 - The storefront

Open `http://localhost:5173/products/<PRODUCT>` signed in as the customer: the **Questions** section under the
reviews has the ask form. As the seller the same page has an answer box on each question and no ask form;
`/shop/questions` lists what waits. As an administrator, `/admin/questions` has *To answer* (the shop's own),
*Visible* and *Hidden*, and hiding asks for a reason. Not recorded whether this walk-through was done by hand for
the merge; the component tests in scenario 1 cover the drawing rules.
