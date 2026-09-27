# Research: Shoppers report a review, a question or a product

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #199

---

## D1 - Catalog owns reports

**Decision**: `content_reports` lives in Catalog's database, and Catalog serves the report, queue and dismiss routes.

**Rationale**: Reviews, questions and products all live in Catalog, so a report can be checked against what it names
(is it on the shelf, is it the reporter's own) with no call to another service. The actions that resolve a report
(hide, take down) are Catalog's too, which lets the close share their transaction (D3). The issue asked this question
directly: "Catalog, where all three kinds live, or a new one?"

**Alternatives considered**:

- **A new service.** Rejected. It would ask Catalog, synchronously, whether each target exists and whose it is. It could
  not close reports atomically with a hide in another database, only eventually, through a message.
- **Activity**, which already holds the audit log and the notices. Rejected: the same cross-service problem.

---

## D2 - Always wait for a person

**Decision**: No threshold hides anything automatically. However many reports a thing has, a moderator decides.

**Rationale**: Hiding a review recomputes the product's rating (specs/046), and taking a product down stops a seller
selling. A threshold turns a few coordinated accounts into a way to do either, and the account rules (one open report
each) do not stop several accounts from reporting together. The queue is ordered by count, so the most reported thing is
seen first without being acted on.

**Alternatives considered**: auto-hide after N reports, pending review. Rejected for the brigading reason above. It can
be revisited with evidence of a queue too long to work through.

---

## D3 - Acting is the existing hide or take-down, which closes the reports

**Decision**: `ContentReports.CloseAsync` is called inside the stage callback of `HideReviewCommand`,
`HideQuestionCommand`, `HideAnswerCommand` and a take-down (`MoveAsync` with `ProductTakenDown`). One guarded
`UPDATE ... WHERE "Status" = 'Open' RETURNING "ReporterId"` closes them, and each reporter is notified through the
outbox in the same transaction.

**Rationale**: A moderator may hide a review from the reviews page without ever opening the queue. If only the queue's
buttons closed reports, those reports would stay open for a review already gone. Putting the close in the handlers makes
every route agree. Sharing the transaction means a hide that fails leaves the reports open, and a hide that commits
never leaves them open (constitution III).

**Alternatives considered**:

- **An "action" endpoint on the reports route** that calls the hide and then closes the reports. Rejected: it is a
  second way to hide, and it would be two transactions.
- **Closing by message**, where the hide publishes and a consumer closes. Rejected: pointless inside one service, and it
  opens a window where the queue shows something already hidden.

---

## D4 - One open report per person per thing, by index

**Decision**: A partial unique index `UX_content_reports_open_per_reporter` on (`TargetType`, `TargetId`,
`ReporterId`) `WHERE "Status" = 'Open'`. The insert is `ON CONFLICT (...) WHERE "Status" = 'Open' DO NOTHING`, and zero
rows means 409.

**Rationale**: A check-then-insert lets two taps both pass the check, and the test sends five at once. The index
counts only open rows, so once staff decide, the same person may report the thing again. The thing may have changed
since, or the decision may have been wrong.

**Alternatives considered**: a unique index over every status. Rejected: a dismissed report would make the thing
unreportable by that person forever.

---

## D5 - What the reporter is told

**Decision**: Two notice kinds, `ReportActioned` and `ReportDismissed`. Each carries only `product` and links to the
product page.

**Rationale**: The issue says to word the outcome "without naming the moderator". Naming the product tells the reporter
which report it was. One kind per outcome, rather than one per target and outcome, keeps the wording table small. The
sentence says "what you reported about “{{product}}”", which reads right for a review, a question or a product.
