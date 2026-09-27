# Quickstart: Validating content reports

**Feature**: [spec.md](spec.md) | **Contracts**: [contracts/](contracts/)

## Scenario 1: The tests (SC-001, SC-002)

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~ContentReportTests"
cd ../client
npx vitest run src/components/report src/pages/admin-reports
```

**Expected**: everything passes. Catalog runs 5 tests; the client runs 3 for the button and 4 for the page.

## Scenario 2: Through the gateway (SC-003)

Bruno's `reviews/` requests at seq 12-20 and `security-checks/` requests at seq 64-65 pass. To check by hand:

```bash
curl -s -X POST http://localhost:5000/api/reports -H "Authorization: Bearer $SHOPPER" -H 'Content-Type: application/json' \
  -d "{\"targetType\":\"Review\",\"targetId\":\"$REVIEW\",\"reason\":\"Spam\"}" -o /dev/null -w '%{http_code}\n'   # 201
# the same again prints 409
curl -fsS http://localhost:5000/api/reports -H "Authorization: Bearer $MODERATOR" | jq '.items[0]'
curl -s -X POST "http://localhost:5000/api/reviews/$REVIEW/hide" -H "Authorization: Bearer $MODERATOR" \
  -H 'Content-Type: application/json' -d '{"reason":"Advertising"}' -o /dev/null -w '%{http_code}\n'           # 200
curl -fsS http://localhost:5000/api/notifications -H "Authorization: Bearer $SHOPPER" | jq '.items[0].kind'      # "ReportActioned"
```

## Scenario 3: In the storefront

1. Sign in as a shopper, open a product, and choose **Report** on a review. Pick a reason and send it.
2. Sign in as a moderator and open `/admin/reports`. The review is listed with "1 report".
3. Choose **Hide review** and give a reason. The card goes, and the shopper's bell shows the outcome.

## Scenario 4: Mutations (SC-004)

Apply each change below on its own, run the suite named beside it, and expect it to go red. Then restore the file.

| Mutation | Suite |
| :-- | :-- |
| `HideReviewCommand` does not close the reports | `ContentReportTests` |
| `TryAddAsync` counts every insert as new | `ContentReportTests` |
| The take-down does not close the reports | `ContentReportTests` |
| A hidden question can be reported | `ContentReportTests` |
| The queue is not ordered by count | `ContentReportTests` |
| One's own content can be reported | `ContentReportTests` |
| The button is offered signed out; details untrimmed | `components/report` |
| A product report hides a question; dismiss sends the wrong type | `pages/admin-reports` |
