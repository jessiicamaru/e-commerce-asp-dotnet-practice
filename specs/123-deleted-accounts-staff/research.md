# Research: Staff see a deleted account as deleted

## D1 - Hide by default, show on request

**Decision**: `includeDeleted` defaults to false.

**Rationale**: Staff look people up to act on them; a deleted account is never the answer, and it is a blank row. Keeping them reachable behind a box serves the rare question "did this person delete their account?".

**Alternatives rejected**: Never listing them - a staff member asking about a complaint could not tell "deleted" from "never existed".

## D2 - Refuse in the target lookup

**Decision**: The private `TargetAsync` every moderation command uses throws 409 `AccountDeleted` for a deleted row.

**Rationale**: One place covers lock, unlock, ban, lift, grant and revoke, and a future command that uses it.

**Alternatives rejected**: 404 - the id is real and staff can see it in the list; a 404 would contradict the page.
