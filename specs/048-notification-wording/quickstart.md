# Quickstart: Validating notification wording

> Written on 2026-09-27, after the feature merged (#129), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/messages.md](contracts/messages.md)

The feature is validated by tests on both sides; no service needs to run.

## Prerequisites

- `client/`: `npm ci` done.
- `server/`: the test databases up (`docker compose up -d`) - Order on 5434, Catalog on 5433 (and S3 on
  8333 for Catalog's fixture today), Identity on 5435 - and `DB_PASSWORD` set.

---

## Scenario 1 - Every kind reads in both languages (US1, SC-001)

```bash
cd client
npx vitest run src/utils/notifications
```

**Expected**: every test passes - 43 in the file at the merge, including one generated test per declared
kind per language (`<Kind> reads as a sentence in en|vi, showing what it was sent`), the kinds-match test
(`has words, in both languages, for exactly the kinds the services declare`) and the six #119 cases
(refused shop, approved product, refused product, taken-down product, "1 star" / "5 stars", the fallback).

## Scenario 2 - What the server sends matches the declaration (US2, acceptance 1 and 2)

```bash
cd server
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Order.Tests    --filter FullyQualifiedName~NotificationTests
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Catalog.Tests  --filter "FullyQualifiedName~ProductReviewTests|FullyQualifiedName~ReviewTests"
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~ShopApplicationTests|FullyQualifiedName~ModerationTests"
```

**Expected** (as recorded at the merge): Order 7/7, Catalog 12/12, Identity 14/14. The Order class
includes `Every_kind_in_code_is_declared_for_the_storefront_and_nothing_else_is`.

## Scenario 3 - The fix is what makes it pass (SC-002)

Revert `client/src/utils/notifications/index.ts` to its state before #129 (locally, without committing)
and run Scenario 1.

**Expected**: 16 failures - the five kinds, in both languages, plus the specific #119 cases. Restore the
file afterwards.

## Scenario 4 - Mutations (SC-003)

Each one alone, restored afterwards:

| Mutation | Expected |
| :-- | :-- |
| Order stops sending `tracking` on `ParcelShipped` | red: `ParcelShipped: missing "tracking"` |
| A constant added to `NotificationKind`, not declared | red: the kinds-match test |
| `describeNotification` stops passing `product` | 12 red |
| The hole fallback removed | 1 red |
| A key added to the declaration that the client has no sample for | red: `no sample for "reviewer" - decide how it reads` |

These are the results recorded in #129.

## Scenario 5 - In the storefront

With the stack running, have a moderator refuse a seller's product with a reason, then open the seller's
bell in each language. **Expected**: "“<product>” was not approved: <reason>" in English and the
Vietnamese equivalent - no `{{`. Not recorded as run for the merge; the evidence is Scenarios 1-4.
