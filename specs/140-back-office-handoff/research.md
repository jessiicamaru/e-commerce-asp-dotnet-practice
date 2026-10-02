# Research: A one-time handoff from the storefront to the back office

## D1 - The handoff replaces the password, never the code

**Decision**: A redeemed handoff answers with a two-factor challenge, the same thing the right password answers. The
session is made only by the authenticator's code, from the back office.

**Rationale**: Proof of the password already exists: the storefront session. The second factor has to be fresh, in the
back office, so that a stolen storefront session cannot become a back-office one. specs/138 makes that session staff
only because its code is exchanged from the back office's `Origin`.

**Alternatives rejected**: Redeeming straight to a session. That would turn any storefront session, which is the one a
script on the storefront could ride, into a staff session.

## D2 - The code travels in the fragment

**Decision**: `/auth/callback#code=...`. The back office reads it and removes it with `history.replaceState`.

**Rationale**: A fragment is never sent to a server. It is not in nginx's or the gateway's logs, not in a `Referer`,
and not in a proxy's records. The issue's first sketch used the query string; the fragment keeps every property
without that exposure.

## D3 - Single use, 30 seconds, hash only

**Decision**: `back_office_handoffs` stores the SHA-256 of a 32-byte random code. It lives 30 seconds and is claimed
by one guarded `UPDATE ... RETURNING`, the way reset-password tokens are (specs/061).

**Rationale**: Thirty seconds covers a click and a page load. A database read gives no usable code. Of two redemptions
at once, the guarded statement lets exactly one through.

## D4 - Issued only to staff with two-factor sign-in

**Decision**: A customer gets 403 `NotStaff`; staff without two-factor sign-in get 403 `TwoFactorSetupRequired`.

**Rationale**: The back office is for staff, and a handoff for somebody who could not use the challenge it yields would
only lead to a dead end.

## D5 - Failing safe

**Decision**: If the storefront cannot get a code, it opens the back office anyway; if the back office cannot redeem
one, it shows its ordinary sign-in.

**Rationale**: The handoff is a convenience. Its failure must cost a password, never a way in.
