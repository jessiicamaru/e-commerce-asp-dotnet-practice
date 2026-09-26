# Quickstart: Validating Email

> Written on 2026-09-27, after the feature merged (#143), from the code at that merge, the pull request and
> [docs/features/email.md](../../docs/features/email.md).

**Feature**: [spec.md](./spec.md) | **Contracts**: [messages.md](./contracts/messages.md)

Scenarios 1 to 4 are what the pull request records running; its results are quoted. Scenario 5 is described for
completeness and was not recorded as run.

---

## Prerequisites

```bash
cd server
docker compose up -d                  # includes e-commerce-mailpit: SMTP :1025, inbox http://localhost:8025
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # or ./start-dev.sh
```

With `start-dev`, `server/.env` needs `SMTP_HOST=localhost`, `SMTP_PORT=1025` (and `STOREFRONT_URL` for the links),
as `.env.example` now has. The containers get `SMTP_HOST=mailpit`.

---

## Scenario 1 - The tests (FR-001 to FR-006)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~EmailTests"
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Order.Tests \
  --filter "FullyQualifiedName~NotificationTests.A_paid_order_asks_for_one_confirmation_email_in_its_language_and_a_failed_one_for_none"
```

**Expected**: all pass - `A_request_delivered_twice_is_kept_once_and_sent_once`,
`It_is_written_in_the_language_asked_for_and_Vietnamese_when_there_are_no_words_for_it`,
`A_mail_server_that_is_down_delays_the_email_and_it_goes_out_when_it_is_back`,
`An_email_that_never_gets_through_gives_up_and_says_why`,
`Nobody_to_write_to_and_no_words_to_write_are_failures_not_retries` and the
`Each_failure_waits_longer_up_to_an_hour` theory. At the merge: Identity 93/93 (10 new `EmailTests`), Order
185/185.

---

## Scenario 2 - One confirmation per paid order (US1, SC-001)

Place and pay an order in English through the storefront or `verify-saga.sh`, then open `http://localhost:8025`.

**Expected**: one email to the customer, "Your order xxxxxxxx is paid", with the total and a link to
`/orders/{id}`. Still one after another sweep. The pull request's run: one email, "Your order 01a0d6a2 is paid".

```sql
-- Identity, localhost:5435, ecommerce_identity_db
SELECT "Template", "Language", "Status", "Attempts", "SentAt" FROM outgoing_emails ORDER BY "CreatedAt" DESC LIMIT 5;
```

---

## Scenario 3 - The mail server down, then back (US2, SC-002)

```bash
docker stop e-commerce-mailpit
# place and pay an order in Vietnamese
```

**Expected**: the order settles `Paid`. Its row is `Pending`, `Attempts` 1, `LastError` "Failure sending mail.",
`NextAttemptAt` a minute later. Then:

```bash
docker start e-commerce-mailpit
```

**Expected**: at the next due sweep the email arrives by itself (`Sent`, attempt 2), once. The pull request's run
received:

```text
Subject: Đơn hàng 01a0d6a2 đã được thanh toán

Xin chào Lan,

Cảm ơn bạn đã mua hàng. Đơn 01a0d6a2 đã được thanh toán: 20.416.000 VND.
Chúng tôi sẽ sớm chuẩn bị hàng và báo cho bạn khi gửi đi.

Xem đơn hàng: http://localhost:8088/orders/01a0d6a2-c762-7683-825f-c44d52dd8bfe
```

Stopping Mailpit also lost its in-memory inbox in that run, which is why compose now keeps its mail on a volume.

---

## Scenario 4 - Mutations (Principle V)

Each was applied, observed red, and restored: queueing without `ON CONFLICT` (red); a failed send gives up at once
(2 red); a paid order asks for no email (red).

---

## Scenario 5 - Nothing leaves the machine (US3, SC-004)

```bash
docker inspect ecommerce-identity --format '{{range .Config.Env}}{{println .}}{{end}}' | grep SMTP_HOST
```

**Expected**: `SMTP_HOST=mailpit`. With no mail server at all (the `auth-smoke` CI job), Identity starts, logs a
warning "Email ... could not be sent, attempt N; next at ..." for each due email, and answers every request
normally.
