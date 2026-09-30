# Research: A person deletes their account

## D1 - Anonymise the identity row; delete everything else Identity holds

**Decision**: the `users` row stays, emptied: email `deleted-<id>@deleted.invalid`, first and last name empty, no
phone, a password hash of random bytes nobody knows, two-factor off, no roles, `DeletedAt` set. Addresses, the seller
profile and payout account, shop applications, outgoing emails, sessions, reset and confirmation tokens, two-factor
challenges and recovery codes, and the sign-in counter for that email are deleted.

**Rationale**: the person's id is held in five other databases (orders, payments, reviews, notices, audit entries).
Keeping the row keeps every reference meaningful ("a deleted account") without a foreign key across services, and the
placeholder frees the email's unique index (`lower("Email")`, #49) so it can register again. `.invalid` is reserved
(RFC 2606) and can never receive mail.

**Alternatives rejected**: deleting the row - `user_roles` and staff tools would then meet ids that resolve to nothing,
and a later registration could never collide anyway. Keeping the email hashed - a hash of an email is still
linkable to that email, which is what deletion must end.

## D2 - Identity asks Order live, over gRPC, what blocks the deletion

**Decision**: Order serves gRPC for the first time - `AccountStanding.GetMyStanding`, an empty request answered from
the forwarded token, on a second Kestrel port (5159 with `start-dev`, 6059 published by compose, 8081 in a container)
like Identity, Catalog and Cart. Identity calls it before its transaction; any blocker is a 409, Order unreachable a 503.

**Rationale**: a blocker is a permission ("may this account go now"), and specs/031 settled that a permission is asked
live, never from a read model seconds behind. The empty request is Cart's reason (feature 010): a
`GetStanding(userId)` would let anything on the network ask about anybody.

**The window**: the check and the deletion are two transactions in two services. An order placed between them - the
length of one gRPC call - would be an open order on a deleted account. The deletion revokes every session in the same
transaction, so nothing can follow it; the order still settles, ships and is kept for the books, only without an
address to show the person. Accepted.

**Alternatives rejected**: a message round trip (Identity requests, Order clears or refuses, Identity completes) - a
pending state on the account, a notice for the refusal, and a UI that cannot answer the person now. The storefront
asking Order first - not enforced by any server. Order consuming `AccountDeleted` and refusing then - too late; the
identity is already gone.

## D3 - What each service erases and what it keeps

| Service | Erased | Kept, and why |
| :-- | :-- | :-- |
| Identity | addresses, seller profile, payout account, shop applications, outgoing emails, every token and code, the sign-in counter | the anonymised user row (D1) |
| Catalog | saved products, review eligibility, reports the person filed | reviews and questions without the author's name (D4); a seller's products and shop, closed (D6) |
| Order | the delivery copy's name, street, postal code and phone on each order (the country stays - the tax depends on it); a return's reason (their words) | orders, lines, parcels, returns, voucher uses (the accounts, and a voucher's per-customer limit); a seller's vouchers (disabled) and payouts (without the holder's name) |
| Cart | the cart and its lines; checkout outcomes | nothing |
| Payment | nothing | payments and refunds: ids and amounts only, the books |
| Activity | notifications | the audit entries (the security record), without the person's email in summaries, as an actor, or the snapshots of their profile |

**Rationale**: Decree 13 lets a controller keep what another law requires it to keep; the shop's accounting records
are that. The issue names the rule: kept, "without the person's name, email, phone or addresses".

## D4 - Reviews and questions stay, without the name

**Decision**: the author's name is cleared; the storefront shows "a former customer". The rating and the text stay,
and the product's rating is not recomputed.

**Rationale**: other shoppers relied on them, a seller's answer to a question hangs on it, and with the name gone the
text no longer identifies anybody through the shop.

**Alternatives rejected**: deleting them - ratings would shift and answers would vanish for everybody else. Letting a
person delete a single review of theirs does not exist today (a review is edited, not deleted); it is a separate
feature and is not added here.

## D5 - Staff accounts are refused

**Decision**: an account holding Admin or Moderator gets 409 `StaffAccount`.

**Rationale**: the first administrator is seeded, and the bootstrap closes "as soon as any admin exists" - deleting
the last one would reopen it. A moderator's decisions are in the audit log under their id; the role is revoked by an
administrator first, which is its own audited act.

## D6 - A seller's shop closes, the way staff close one

**Decision**: Catalog sets the shop's `ClosedAt` with the reason "The account was deleted", and `ApplyShopStateAsync`
takes every product off the shelf (specs/107). Order disables the seller's vouchers and clears the holder's name on
their payouts.

## D7 - One message, four consumers, named for what they do

**Decision**: `Ecommerce.Contracts/Identity/AccountDeleted(UserId, Email, DeletedAt)` through Identity's outbox,
beside `AccessTokensRevoked`. Consumers: `EraseAccountFromCatalogConsumer`, `EraseAccountFromOrdersConsumer`,
`EraseAccountFromCartConsumer`, `EraseAccountFromActivityConsumer` - a consumer's class name is its queue name, so four
classes called `AccountDeletedConsumer` would share one queue. Each erasure is idempotent by nature (updates to fixed
values and deletes by id), so a redelivery changes nothing.

**The email in the message**: Activity's summaries were written with the email in them (`"x@y changed their
details"`), and Activity has no other way to find it. The message is the last place the email travels; the outbox
row is deleted once delivered.

## D8 - The inventory says which sections are kept

**Decision**: `PersonalDataInventory` gains `Kept` - the sections that survive a deletion, each with the reason. Every
other exported section is erased. The deletion test in each service seeds a person with a row in every section, erases
them, reads their export, and asserts: every section not kept is empty, and nothing in the export contains the planted
name, email, phone or street.

**Rationale**: specs/111 promised that deletion "reads the same list"; this is how. A new table is declared once and
both the export and the deletion test see it.

## D9 - Re-authentication by password; a refusal carries its reasons

**Decision**: the current password, checked like `PUT /api/auth/me/password` - a wrong one counts toward the sign-in
pause (specs/062) - and the gateway limits the route with the `sign-in` policy. A 409 carries facts, as a 403 does
since specs/049: `ConflictException` gains the same optional facts, written by the shared handler.

**Alternatives rejected**: a code by email - the address is about to be erased and the person is already signed in.
Requiring a two-factor code too - staff are refused anyway (D5), and a customer's second factor is not offered.
