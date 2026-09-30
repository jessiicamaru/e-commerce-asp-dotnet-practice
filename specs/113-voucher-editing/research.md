# Research: A voucher's terms can be corrected

## D1 - Which terms are editable

**Decision**: the name, the end date, the total and per-customer limits, each existing currency's minimum subtotal, and
the value of an existing minimum-quantity condition. Nothing else: not the code, benefit, percentage, fixed amount,
cap, currencies, targets, the other conditions, or the start date.

**Rationale**: the issue's line - "what does not change a past order's price". Every redemption freezes its code, name,
benefit and amount (`voucher_redemptions`, specs/069), so none of these edits reaches an order already placed. What a
voucher takes off is what the shopper was shown; changing it under them, even for future orders, is a different
voucher. Adding a currency or a condition changes who it applies to in ways a correction does not need, and the
targets decide which products it applies to at all.

**Alternatives rejected**: editing everything while `UsedCount = 0` - a shopper may have seen it without using it
yet. Versioning a voucher's terms - there is no reader for the history beyond the audit log, which already has it.

## D2 - One guarded statement for the limits

**Decision**: `UPDATE vouchers SET ... WHERE "Id" = @id AND "Status" = 'Active' AND (@total IS NULL OR "UsedCount" <=
@total)` (with the owner's condition), plus the amounts' and the condition's rows, in one transaction with the audit
entry. Zero rows: read the voucher to say why - 404 (not there, not yours), 409 disabled, 409 below the uses.

**Rationale**: a checkout claims a use with `"UsedCount" < "TotalLimit"` in a guarded statement (specs/069 research
D5). Both statements take the row's lock, so they serialise: a lower limit is written only if it still covers the uses,
and a claim after it sees the new limit. A read-then-write would let both pass.

## D3 - Who edits

**Decision**: a seller edits their own; an administrator edits the platform's (`SellerId` null). Anything else is 404.

**Rationale**: a seller pays for their own voucher (specs/069, `GoodsTotal` is net of it). An administrator disabling a
seller's voucher is moderation and stays possible; rewriting its terms is the seller's business decision. The pages
already list exactly these - each role's own.

## D4 - The end date

**Decision**: null (open-ended) or after both the start and now.

**Rationale**: an end in the past is "stop now", which `disable` already does and audits as such; two ways to do it
would read differently in the log for the same act.

## D5 - No concurrency token

**Decision**: the last edit wins; the audit log keeps both with their before and after.

**Rationale**: the limits are protected by D2 whatever the order of edits; the rest are an owner correcting their own
words and dates. An `expectedUpdatedAt` would make a correction fail for no harm prevented.
