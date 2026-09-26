# Research: A Catalogue of Real Cameras

> Written on 2026-09-27, after the feature merged (#60), from the code at that merge, the pull request
> and docs/features/catalog.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-22

Eight decisions, reconstructed from the pull request, the seeder's docstrings and the comments on the
Catalog fixes. Who took each is not recorded beyond the pull request's author.

## D1 - Seed through the API, not through SQL

**Decision**: every row goes through the gateway, signed in as the seeded administrator.

**Rationale**: "Every row goes down the path a person uses, so seeding exercises validation, the
outbox, the availability announcements and the price rules rather than stepping around them. A seeder
that writes straight to the database is the one place a catalogue can hold something the API would have
refused." It is also why the seeder found two defects nothing else had (D5, D6).

**Alternatives considered**:

- **SQL or an EF seed.** Rejected for the reason above; it would also skip the outbox, so Inventory
  would never hear of the variants.

## D2 - The data in a JSON file, in both languages and both currencies

**Decision**: `server/seed/cameras.json` - 2 categories, 14 products, 23 variants; per product
Vietnamese and English name and description; per variant a SKU, options as `[vi-name, vi-value]` and
`[en-name, en-value]`, a `vnd` price, a `usd` price and a stock count. Vietnamese is the default
language and dong the default currency, so the product is created with those and the English text and
dollar prices are written afterwards, each in its own request.

**Rationale**: the shop's defaults (specs/021, specs/022) - the product's own columns are the
default-language text and `product_variants.Price` the default-currency amount.

**Alternatives considered**: not recorded.

## D3 - Prices that are approximate, said so, and never converted

**Decision**: prices "from a model's knowledge up to May 2026, not from any shop", with the warning at
the top of `cameras.json` (`_about`), printed by the seeder at start, and in CLAUDE.md. The two lists are
set independently.

**Rationale**: "a number that looks researched and is not is worse than no number." They are "the right
order of magnitude and the right relative order - an A7 IV costs more than a ZV-E10 II and less than a
Z6III - which is what makes the catalogue useful to click through." Not conversions, because specs/022
stores two lists: "45,000,000₫ / $2,198 is about 20,500 to the dollar, 18,500,000₫ / $799 is about
23,200. That spread is real."

**Alternatives considered**: one list and a rate. Rejected by specs/022's design.

## D4 - Idempotent by SKU, never deleting, and finishing what a failed run started

**Decision**: categories are matched by slug, products by product SKU, variants by variant SKU; the
first variant is the product's own (specs/020: it reuses the product id and carries the product's SKU).
Translations, dollar prices and stock are upserts and are rewritten every run. Nothing is deleted.

**Rationale**: "Running it twice adds nothing; running it after a failure finishes the job ... cheap and
it makes a half-finished run recoverable." "What is already in the catalogue is not this script's to
remove, and there is no endpoint that would let it."

**Alternatives considered**: removing the 94 junk products by SQL. Rejected: "destructive, and not
something to do without being asked".

## D5 - A variant option carries its id

**Decision**: `VariantOptionResponse(Guid Id, string Name, string Value)`; the client type gains `id`.

**Rationale**: specs/021 added `PUT /api/products/{id}/options/{optionId}/translations/{lang}` and "no
response carried an option id. Nothing outside the database could call it. The seeder is the first API
client that ever tried."

**Alternatives considered**: not recorded. (Addressing an option by name instead of id would change
the endpoint that shipped; the fix is additive.)

## D6 - Options in one order: by stored name, case-insensitive

**Decision**: `ProductVariant.Summarise` orders by `Name` with `StringComparer.OrdinalIgnoreCase`;
`Localized` orders by the **stored** name too and substitutes the translated words. A data-only
migration rewrites every stored `OptionSummary` with `string_agg(... ORDER BY lower(o."Name"))`; its
`Down` is empty.

**Rationale**: "Nothing ordered the option rows", so the same variant could read `Colour: … · Kit: …`
once and `Kit: … · Colour: …` the next. Worse, "a read that named a language rebuilt the summary from
the rows while a read that named none used the stored string" - and the stored string is what the gRPC
pricing call freezes onto an order line. Ordering by the translated name "would reshuffle the summary
when the language changed". Without the migration, "the same variant reads two different ways".

**Alternatives considered**:

- **Order by the translated name.** Rejected: above.
- **Fix only new summaries.** Rejected: the stored ones would keep disagreeing with the rebuilt ones.
- **A reversible migration.** Rejected: "the previous order was whatever the database happened to
  return, which is not a state anything can be restored to".

## D7 - Match an option to its translation by name, never by position

**Decision**: the seeder builds `{vi-name: en-pair}` for the variant and looks each stored option up by
its name.

**Rationale**: "The first version of this paired `stored[i]` with `wanted[i]`, and the response does not
come back in the order the options were entered - so `Bộ: Chỉ thân máy` was given the English `Colour:
Black`. ... A list of things with identities is never matched by index." Caught by reading the rows.

**Alternatives considered**: position. Rejected by the bug above.

## D8 - Wait for Inventory; invent no images

**Decision**: `set_stock` tolerates a 404 from `PUT /api/stock/{variantId}` and retries up to 20 times,
0.5 s apart, then stops with "Inventory never registered variant ...". Products are seeded with no
image.

**Rationale**: "Inventory learns about a variant through the broker, not through this call ... A stock
write sent in between is a 404 about a variant that certainly exists - which is correct of Inventory."
Seen on the second run and not the third: "exactly the kind of thing that passes locally and fails in
CI". "Recorded in the code rather than papered over as a flake." No images because "there is no honest
way to obtain product photographs here, and a placeholder that looks like a photograph is worse than a
blank"; the storefront already handles `imageUrl: null` (specs/019).

**Alternatives considered**: treating the 404 as an error (the flake above); placeholder images
(rejected above).
