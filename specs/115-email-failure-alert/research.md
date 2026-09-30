# Research: Administrators are told when an email fails for good

## D1 - A digest, at most one an hour

**Decision**: one `EmailsFailed` notice per administrator per sweep that finds uncounted failures, and none within an
hour of the last; the next one counts everything since.

**Rationale**: failures come in bursts - an SMTP outage fails every queued email over a few hours as each reaches its
12th attempt. A notice per email would put dozens in the bell; the issue allows a digest. An hour is the dispatcher's
longest retry interval (`LongestRetry`), so an outage is reported about as often as it is retried.

**Alternatives rejected**: a notice per email (floods); a daily digest (a password reset that failed at 09:00 is urgent
by 10:00).

## D2 - The mark is on the email, and one statement claims

**Decision**: `outgoing_emails.FailureAlertedAt`. The claim is
`UPDATE outgoing_emails SET "FailureAlertedAt" = @now WHERE "Status" = 'Failed' AND "FailureAlertedAt" IS NULL AND NOT
EXISTS (SELECT 1 FROM outgoing_emails WHERE "FailureAlertedAt" > @now - interval '1 hour')`, returning the rows.

**Rationale**: the mark says exactly which failures a notice counted, so none is counted twice and none lost; two
instances sweeping at once meet on the rows' locks, and the second finds them already marked (READ COMMITTED re-checks
`"FailureAlertedAt" IS NULL` on a row another transaction changed). No separate table of "last alert", which would be a
second thing to keep consistent with the first.

**Alternatives rejected**: a single-row `email_alerts` table with the last time - the count would have to be worked out
from `UpdatedAt`s that other writes move. Counting failures by time windows - a failure landing on the boundary is
counted twice or never.

## D3 - In the dispatch's transaction, through the outbox

**Decision**: the claim and the notices (`INotifier.NotifyAsync`, one per administrator, through Identity's outbox) are
staged after the sweep's own save and committed with it.

**Rationale**: Principle III - the mark and the notice commit together or not at all; a crash between them would either
lose a notice (marked, not sent) or double it (sent, not marked).

## D4 - Who is told

**Decision**: every user holding `Admin` (not Moderator - the email pages are Admin only, specs/087).

## D5 - The Overview reads the email log

**Decision**: the Overview asks the existing `GET /api/emails?status=Failed&pageSize=1` and shows its total.

**Rationale**: the log's total is exactly "how many are failed"; a second endpoint would be a second definition.
