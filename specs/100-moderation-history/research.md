# Research: Staff see a person's moderation history

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #198

---

## D1 - The publisher names the person on the entry

**Decision**: `AuditEntryRecorded` gains `Guid? AboutUserId = null`, set by the service that makes the decision. That
service has the review, question, product or application in hand, and so knows whose it is. `AuditTrail` fills it from
the subject id when the subject is a `User`, so Identity's lock, ban, unlock and role entries need no change at their
call sites.

**Rationale**: The issue asks for exactly this choice: "linking them to a person needs the author on the entry, or a
lookup at read time". A lookup at read time would make Activity call Catalog (review authors, question askers, product
sellers) and Identity (applicants) for every page of history. That is a synchronous edge from the audit service into two
others, and the answer would be today's owner rather than the owner at the time of the decision. The publisher already
holds the fact, inside the same transaction as the decision.

**Alternatives considered**:

- **A lookup at read time.** Rejected for the reasons above, and because a deleted review or product would have no author
  left to look up.
- **Encoding the person into `SubjectId`.** Rejected: it would change what every existing reader of the subject gets.

The contract change is additive. The new trailing parameter has a default, MassTransit's JSON deserialises a missing
property to null, and an older Activity ignores the extra field. Neither direction of a rolling deploy breaks.

---

## D2 - Moderators may read one person's Moderation entries

**Decision**: `GET /api/audit/people/{userId}` is `StaffRoles.Staff`. The handler fixes the category to `Moderation`; the
category is not taken from the request. The response carries the action, actor, subject, summary, time and a `Reason`
read out of the "after" snapshot, and never the snapshots or the diff.

**Rationale**: The rest of the audit log is an administrator's (specs/041). A moderator needs the decisions, not the data
around them. A Security entry would show sign-in times and addresses, and an Order entry what somebody bought. Fixing
the category in code, not in a parameter, means no query string can widen the route. Leaving the snapshots out keeps
whatever else an entry captured, such as roles and dates, on the administrator's side of the line.

**Alternatives considered**:

- **Opening `GET /api/audit?category=Moderation` to moderators.** Rejected: the same handler would then serve moderators
  and administrators with a filter as the only difference, which is one missed check away from the whole log.
- **Returning the snapshots.** Rejected: the reason is the one field a moderator needs.

The reason is the first top-level string property whose name ends in "reason": `reason` on a hidden review or a rejected
application, `lockReason` or `banReason` on an account. That covers every Moderation publisher today. A new publisher
that stores its reason under another name shows no reason until it follows the pattern.

---

## D3 - Backfill User subjects, leave content alone

**Decision**: The migration sets `AboutUserId = SubjectId::uuid` where `SubjectType = 'User'` and the id is a uuid.
Nothing else is backfilled.

**Rationale**: Locks and bans from before this feature are what a moderator most needs to see, and their subject is the
person. A content entry's subject is a review or product whose author Activity never recorded. Guessing, or asking
Catalog today, would attach yesterday's decision to today's owner. On the development database the backfill set 1,500 of
1,520 User-subject rows. The other 20 are refused sign-ins for unknown addresses, which have no subject.

---

## D4 - Where the history is drawn

**Decision**: The lock and ban dialog shows the latest five decisions (with "and N earlier") above the reason. A
"History…" menu item opens the whole history, ten per page.

**Rationale**: The issue's acceptance is about the moment of deciding, so the history belongs inside the dialog where
the decision is typed. A separate page would be one more click that nobody takes when in a hurry. The menu item is for
reading without deciding.

---

## D5 - Which entries name a person

**Decision**: Decisions staff make about someone's content name that person: reviews, questions and answers hidden or
restored, products approved, rejected or taken down, and shops approved or rejected. The seller's own
`ProductSentForReview` and `ProductResubmitted` do not.

**Rationale**: The history answers "what have we decided about this person". A seller editing their own product is not a
decision about them, and it would fill the list with noise at every edit. Restorations are included because a hide
followed by a restore reads differently from a hide alone.
