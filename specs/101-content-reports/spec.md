# Feature Specification: Shoppers report a review, a question or a product

**Feature Branch**: `101-content-reports` | **Created**: 2026-09-27 | **Issue**: #199

**Status**: Merged (#208, 2026-09-27)

**Input**: Issue #199 - "shoppers cannot report a review, a question or a product".

## Why

A shopper who sees an abusive review, a spam question or a counterfeit listing has no way to say so. Moderators find
problems only by reading everything themselves. There is no `content_reports` table and no report endpoint.

## User Scenarios & Testing *(mandatory)*

### US1 - A shopper reports something (Priority: P1)

A signed-in shopper chooses "Report" on a review, a question or a product page. They pick a reason from a short list
(spam, offensive, misleading, counterfeit, other), may add a few words, and send it.

**Why this priority**: Without it there is nothing to queue.

**Independent Test**: Report a review; it appears in the moderators' queue. Report it again; the second report is refused.

**Acceptance Scenarios**:

1. **Given** a review on sale, **When** a shopper who did not write it reports it, **Then** the report is recorded
   (201).
2. **Given** an open report by the same person on the same thing, **Then** a second report is refused (409), including
   when two arrive at once.
3. **Given** a hidden review or question, or a product off the shelf, **Then** reporting it is 404 - only what a
   shopper can see can be reported.
4. **Given** the shopper's own review, question or product, **Then** reporting it is refused (409).

---

### US2 - Staff work through what was reported (Priority: P1)

`/admin/reports` shows one card per reported thing: how many reports it has, the reasons, the reporters' words, and what
it says. The most reported thing comes first, then the one waiting longest. On each card staff either act or dismiss:

- **Act** uses the existing hide or take-down: "Hide review", "Hide question" or "Take product down", with a reason.
- **Dismiss** ("No action") leaves the thing as it is.

Either way, every open report of the thing closes, and each reporter is told how it ended, never by whom.

**Why this priority**: This is the issue's acceptance.

**Acceptance Scenarios**:

1. **Given** a review reported twice, **When** a moderator hides it from the queue or from the reviews page, **Then**
   both reports close as actioned, in the hide's own transaction, and both reporters receive "ReportActioned".
2. **Given** reports of a product, **When** staff dismiss them, **Then** they close as dismissed, each reporter
   receives "ReportDismissed", the decision is audited, and the product stays on sale. Dismissing again is 409.
3. **Given** a question reported, **When** staff hide only its answer, **Then** the question's reports close as
   actioned, because a question is reported together with its answer.
4. **Given** reports that were decided, **Then** the same person may report the thing again.

### Edge Cases

- **Many reports on one thing.** Nothing is hidden automatically. A person always decides (Decision 2).
- **A product deleted.** Its reports go with it (cascade on `ProductId`), including reports of its reviews and questions.
- **Staff acting from another page.** Hiding from `/admin/reviews` or `/admin/questions`, or taking down from
  `/admin/products`, closes the reports just the same. The close lives in the handlers, not in the queue's page.
- **Rejecting a pending product.** It cannot have been reported, because it was never on the shelf, so only a take-down
  closes a product's reports.
- **Who decided.** The notices carry only the product's name, never the moderator.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `POST /api/reports` (signed in) takes `targetType` (Review, Question or Product), `targetId`, a `reason`
  from the list and optional `details` (at most 500 characters). The reporter comes from the token.
- **FR-002**: A person has one open report per thing, enforced by a partial unique index on the open rows.
- **FR-003**: Only what a shopper can see can be reported, and never one's own review, question or product.
- **FR-004**: `GET /api/reports` (Staff) lists the open reports grouped per thing, most reported first. Each row carries
  the count, the reasons, the latest few details and an excerpt.
- **FR-005**: Hiding a review, question or answer, or taking a product down, closes the thing's open reports as
  `Actioned` in the same transaction and tells each reporter (`ReportActioned`).
- **FR-006**: `POST /api/reports/{targetType}/{targetId}/dismiss` (Staff) closes them as `Dismissed` and tells each
  reporter (`ReportDismissed`). It is audited as `ReportsDismissed`, and dismissing when nothing is open is 409.
- **FR-007**: The storefront shows "Report" on reviews, questions and the product page to anyone signed in, adds a
  `/admin/reports` page for staff with a menu entry, and words the two new notices in en and vi.

### Key Entities

- **Content report** (Catalog): the target, its product, the reporter, the reason and details, and a status (Open,
  Actioned or Dismissed) with who closed it and when.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Catalog tests cover:
  - a report reaches the queue, and hiding the review closes it and tells each reporter;
  - a second open report is refused, including five sent at once;
  - dismissing closes the reports, tells each reporter and is audited, and the person may then report again;
  - only visible content can be reported, and never one's own;
  - a take-down or an answer hidden closes their reports;
  - the queue orders by count.
- **SC-002**: Storefront tests cover:
  - the button is not offered signed out, and it sends the reason with the words trimmed;
  - a refusal is shown in the server's words;
  - the queue shows counts, reasons and words, hides a review with a reason, takes a product down and dismisses.
- **SC-003**: Bruno: a shopper reports a review, a second report is 409, staff see it and dismiss it (again is 409), it
  is reported again, hiding closes it, and the queue no longer holds it. Without a token is 401; a customer reading
  the queue is 403.
- **SC-004**: Each of these mutations makes a test fail: the review hide not closing reports, duplicates accepted,
  the take-down not closing, a hidden question reportable, the queue not ordered by count, one's own content
  reportable, and four client mutations.

## Decision

1. **Catalog owns reports.** All three kinds of content live there, and so do the actions that resolve them
   ([research.md](research.md) D1).
2. **Always wait for a person.** Hiding automatically after a number of reports would let a few accounts silence a
   review or move a seller's rating (D2).
3. **Acting is the existing action.** The hide and take-down handlers close the reports, so the queue adds no second way
   to hide something (D3).

## Assumptions

- A reason list of five covers what shoppers report; "Other" plus the free-text details covers the rest.

## Out of scope

- Reporting a seller or a shop as such; reporting an answer separately from its question; rate-limiting reports beyond
  one open report per person per thing.
