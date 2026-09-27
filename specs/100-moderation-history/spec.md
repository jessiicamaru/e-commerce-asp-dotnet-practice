# Feature Specification: Staff see a person's moderation history

**Feature Branch**: `100-moderation-history` | **Created**: 2026-09-27 | **Issue**: #198

**Status**: Merged (#207, 2026-09-27)

**Input**: Issue #198 - "a moderator decides a lock without seeing the person's history".

## Why

A moderator about to lock somebody sees the person's roles and current state, but nothing that happened before: earlier
locks and their reasons, reviews hidden, shop applications rejected. Every decision is made as if it were the first. The
audit log holds that history (specs/041), but only administrators can read it (`GET /api/audit`). Moderators can read
their own decisions (`/api/audit/mine`), not what anyone decided about the person in front of them.

A second gap sits underneath. An entry about a person's content is keyed by the content's id: a hidden review names the
review, and a product taken down names the product. Nothing in the log connects those entries to the person who wrote or
sold the thing.

## User Scenarios & Testing *(mandatory)*

### US1 - The history is where the decision is made (Priority: P1)

When a moderator or administrator opens the lock or ban dialog for a person, the dialog lists the latest decisions staff
made about that person: what was decided, when, by whom and why. If there are more than five, it says how many more.

**Why this priority**: This is the issue's acceptance: "a moderator sees, before locking, that the person was locked twice
before and why".

**Independent Test**: Lock a customer, unlock them, then open the lock dialog for them again. The earlier lock and its
reason are listed above the form.

**Acceptance Scenarios**:

1. **Given** a person locked twice before, **When** a moderator opens the lock dialog, **Then** both locks are listed
   with their reasons, newest first.
2. **Given** a person nobody decided about, **Then** the dialog says there is nothing on record.

---

### US2 - The whole history, from the person's menu (Priority: P2)

Each person's row on `/admin/users` has a "History…" menu item. It opens every moderation decision about the person,
paged.

**Why this priority**: The dialog shows five. Reading further back is worth having, but it is not what blocks a decision.

**Independent Test**: Open the menu and choose "History…". The dialog lists the decisions and pages through them.

---

### US3 - Content decisions count toward the person (Priority: P1)

A review hidden, a question or answer hidden, a product rejected or taken down, and a shop application decided are all in
the history of the person whose content it was.

**Why this priority**: Without it, the history holds only locks and bans, and a serial spammer whose reviews staff hid ten
times looks clean.

**Acceptance Scenarios**:

1. **Given** a customer whose review was hidden, **Then** their history lists "Review hidden" with the reason.
2. **Given** a seller whose product was taken down, **Then** it is in the seller's history.

### Edge Cases

- **Other parts of the log.** Security entries (sign-ins), Order entries and User entries (password changes) about the
  same person are never returned by this route. The category is fixed on the server and is not a parameter.
- **Entries from before this feature.** Entries whose subject is a user (locks, bans, unlocks, role changes) are
  backfilled from their subject id. Content decisions from before cannot be backfilled: Activity never knew who wrote a
  review or sold a product. Those entries stay in the administrator's log without a person attached.
- **An older publisher.** A service image from before this feature sends no `AboutUserId`, and the message deserialises
  with null.
- **The person's own acts.** A seller editing their product sends it back to review (`ProductSentForReview`), but that is
  the seller's act, not a decision about them. It is not attached.
- **Snapshots.** They are not returned. The reason is read out of the "after" snapshot and nothing else is.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `AuditEntryRecorded` gains an optional `AboutUserId`, the person an entry is about. `IAuditTrail.RecordAsync`
  takes an optional `aboutUserId` and fills it with the subject id itself when the subject type is `User`.
- **FR-002**: Every moderation decision about someone's content names that person: `ReviewHidden` and `ReviewRestored`
  name the review's author; `QuestionHidden` and `QuestionRestored` the asker; `AnswerHidden` and `AnswerRestored` the
  answer's author; `ProductApproved`, `ProductRejected` and `ProductTakenDown` the product's seller; `ShopApproved` and
  `ShopRejected` the applicant.
- **FR-003**: Activity stores `audit_entries.AboutUserId`, indexed on (`AboutUserId`, `OccurredAt`), and a migration
  backfills it for User-subject rows.
- **FR-004**: `GET /api/audit/people/{userId}` (Admin, Moderator) returns that person's Moderation entries, newest first
  and paged. Each entry carries action, actor, subject, summary, reason and time, and no snapshots or diff.
- **FR-005**: `/admin/users` shows the latest five decisions in the lock and ban dialog, and the whole history from a
  "History…" menu item.

### Key Entities

- **Audit entry** (Activity): gains the person it is about.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Activity tests:
  - the history lists a person's decisions newest first, with their reasons;
  - it excludes other people's decisions;
  - it never returns entries outside Moderation;
  - the trail fills `AboutUserId` from a User subject and takes it when given.
- **SC-002**: Publisher tests: hiding a review, question or answer, taking down a product, rejecting a shop application
  and locking an account each carry the right person.
- **SC-003**: Storefront tests: the lock dialog lists earlier decisions with their reasons and a count of the rest, says
  when there are none, and the menu's history pages.
- **SC-004**: Bruno: a moderator reads a customer's history after locking and unlocking them, and it holds no sign-in; a
  customer is refused.
- **SC-005**: Each of these mutations makes a test fail:
  - the category filter dropped;
  - the trail's User-subject fallback dropped;
  - the reason reader narrowed to `reason` only;
  - a review hide without its author;
  - the lock dialog without the history;
  - the history dialog stuck on page 1.

## Decision

1. **The person is written on the entry, not looked up at read time.** The publisher knows the author; Activity does not.
   A lookup would mean Activity calling Catalog and Identity for every row ([research.md](research.md) D1).
2. **Moderators read it too**, but only Moderation entries about one person at a time, never the rest of the log (D2).
3. **Backfill what can be backfilled**, meaning User subjects. The rest is left alone rather than guessed (D3).

## Assumptions

- The reasons staff give are not secret, since the person reads them too (notices, specs/059), so showing them to other
  staff is no new exposure.

## Out of scope

- Reports from shoppers (#199); notes staff write about a person outside a decision; a history for administrators' own
  accounts beyond what the log already shows.
