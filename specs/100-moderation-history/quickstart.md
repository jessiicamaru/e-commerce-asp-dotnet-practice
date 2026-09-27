# Quickstart: Validating a person's moderation history

**Feature**: [spec.md](spec.md) | **Contracts**: [contracts/](contracts/)

## Scenario 1: The tests (SC-001, SC-002, SC-003)

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Activity.Tests
DB_PASSWORD=... dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~ReviewTests|FullyQualifiedName~ProductQuestionTests|FullyQualifiedName~ProductReviewTests"
DB_PASSWORD=... dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~ModerationTests|FullyQualifiedName~ShopApplicationTests"
cd ../client
npx vitest run src/pages/admin-users
```

**Expected**: everything passes. Activity runs 40 tests, 4 of them new: 3 `PersonHistoryTests` and 1 in
`AuditTrailTests`. The users page runs 13 tests, 3 of them new.

## Scenario 2: Through the gateway (SC-004)

Bruno's `admin-users/` seq 15 and `security-checks/` seq 63 pass. To check by hand, after a moderator has locked and
unlocked a customer:

```bash
curl -fsS "http://localhost:5000/api/audit/people/$CUSTOMER_ID" -H "Authorization: Bearer $MODERATOR" | jq '.items[] | {action, reason}'
curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5000/api/audit/people/$CUSTOMER_ID" -H "Authorization: Bearer $CUSTOMER"
```

**Expected**: the first command lists `AccountUnlocked` and then `AccountLocked` with its reason, and no `SignedIn`. The
second prints `403`.

## Scenario 3: The backfill (FR-003)

```bash
docker exec ecommerce-activity-db psql -U postgres -d ecommerce_activity_db -tAc \
  "select \"SubjectType\"='User', count(*) filter (where \"AboutUserId\" is not null), count(*) from audit_entries group by 1"
```

**Expected**: User-subject rows carry `AboutUserId`, except refused sign-ins for unknown addresses, which have no subject.
On the development database the count was 1,500 of 1,520.

## Scenario 4: In the storefront (SC-003)

1. Sign in as a moderator and open `/admin/users`.
2. Open "Lock…" for someone who was locked before. Their earlier decisions are listed above the reason, with the
   reasons in quotes.
3. Choose "History…" from the same menu. Every decision is listed, paged.

## Scenario 5: Mutations (SC-005)

Apply each change below on its own, run the suite named beside it, and expect it to go red. Then restore the file.

| Mutation | Suite |
| :-- | :-- |
| The repository drops `Category == category` | `PersonHistoryTests` |
| `AuditTrail` drops the User-subject fallback | `AuditTrailTests` (and Identity `ModerationTests`) |
| `ReasonIn` reads only a property named `reason` | `PersonHistoryTests` |
| `HideReviewCommand` records without `aboutUserId` | Catalog `ReviewTests` |
| The stop dialog without `PersonHistory` | `pages/admin-users` |
| The history dialog always asks for page 1 | `pages/admin-users` |
