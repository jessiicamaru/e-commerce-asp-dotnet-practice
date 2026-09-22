# Research: Two Price Lists, Not One Price Converted

The spec named six decisions to take before building. This is what they were settled as, and why the
rejected alternatives were rejected.

## D1 - Where the currency is decided, and how a request asks for one

**Decision**: a shared `IRequestCurrency` in `Ecommerce.Shared`, beside `IRequestLanguage` and read
the same way, resolved per request in this order:

1. `?currency=USD` on the query string - one address, one currency, so a link carries it;
2. a `X-Currency` request header, which is what the storefront sends on every call;
3. the configured default (`Money:DefaultCurrency`, `VND`).

An unsupported code falls back to the default rather than 404, exactly as an unsupported language
does (FR-004).

**Why a custom header rather than reusing content negotiation**: there is no standard request header
for currency. `Accept-Language` exists because a language is a property of the *representation*;
a currency is a property of the *offer*, and HTTP has never had an opinion about it. Inventing a
meaning for a standard header would be worse than a clearly non-standard one, so the header is
`X-Currency` and the query string is the shareable form.

**Rejected: deriving the currency from the language.** It is one line of code and it is wrong in both
directions. Most of this shop's customers are Vietnamese people who read English; they pay in dong.
A visitor who reads Vietnamese may still want dollars. Language is what you read; currency is what
you pay. Conflating them means a shopper cannot have one without the other.

**Rejected: deriving it from the delivery address.** The address is chosen in the middle of checkout,
after the shopper has seen prices and filled a cart. Deriving the currency from it would change every
price they had already agreed to, at the last step.

**Rejected: deriving it from IP geolocation.** Same objection as above plus a worse one - it cannot be
overridden by the person it is wrong about, and it is wrong for every traveller and every VPN.

## D2 - Whether a price list hangs off the product or the variant

**Decision**: off the **variant**. `variant_prices (VariantId, Currency, Amount)`, unique on
`(VariantId, Currency)`.

The variant is the sellable unit (specs/020): it carries the SKU, the stock and today's single price.
A price list on the product would have to be divided among variants by some rule, and there is no
rule - a body and a kit are different prices in both currencies, independently.

`product_variants.Price` **stays**, holding the default-currency amount. It is the fallback for the
default currency, what an image built before this feature reads, and what makes the change additive.
`products.Price` - the "from" price, the cheapest active variant - stays too, and gains a per-currency
equivalent computed the same way.

**Rejected: `PriceVnd`/`PriceUsd` columns.** A third currency becomes a migration, which FR-009 and
the spec's own test both forbid.

**Rejected: storing one price plus a rate per currency.** That is conversion with extra steps, and it
is what US2 exists to refuse.

## D3 - What happens when a variant is not priced in the requested currency

**Decision**: it is **not sellable in that currency**, and the catalogue says so rather than hiding it
or converting.

This is the single most important difference from the translation design in specs/021, and the two
look similar enough to be worth writing down:

| | A missing translation | A missing price |
| :-- | :-- | :-- |
| Falls back? | Yes, per field, to the default language | **No, never** |
| Worst case if it did | A shopper reads English | A camera sells for 1,600 dong, or costs 40,000,000 dollars |

So:

- **In a listing**: the variant comes back with a **null price** and `sellable: false`. The product is
  still listed, because hiding it would make a half-priced catalogue look like a half-empty shop and
  give an administrator no way to notice.
- **At checkout**: `PriceVariants` is all-or-nothing, as it already is for an unknown variant. A
  variant with no price in the checkout's currency is reported as unsellable and the checkout refuses
  with 409, naming it - the same refusal path as "not currently for sale", which already exists.
- **The product's "from" price** in a currency is the cheapest variant **that has a price in it**, and
  is null when none does.

**Rejected: falling back to the default currency's amount.** Covered above. The failure is silent, the
number looks plausible in a listing, and the first person to notice is whoever reconciles the money.

**Rejected: 404 for the product.** It exists; it is just not sold in dollars yet. Reporting "not
found" would send an administrator hunting for a deleted product.

## D4 - How the currency reaches Payment

**Decision**: `OrderSubmittedEvent` gains `Currency`, the saga stores it on `OrderStateData`, and
relays it into `ProcessPaymentCommand`. Payment stores it on the row.

**This crosses the exact relay that specs/020 broke.** The saga consumes `OrderSubmittedEvent` and
republishes parts of it; when `OrderItemDto` gained `VariantId`, every service was rebuilt except the
Orchestrator, which deserialised the event into its older record, dropped the field, and moved the
wrong variant's stock. The identical omission here would charge the right number in the wrong
currency, and **nothing downstream could detect it** - `899` is a valid amount in both.

Two consequences, both deliberate:

1. The Orchestrator image **must** be rebuilt with this change, and the rebuild is a task in its own
   right rather than an assumed side effect.
2. `ProcessPaymentCommand.Currency` is a `string` defaulting to `""`, and Payment reads `""` as the
   shop's default currency - so an in-flight message from an older Order still resolves to what it
   actually meant. An empty currency is **not** an error, because on the day of the deploy it is the
   truth.

**Rejected: Payment reading the currency back from Order.** It would make Payment synchronously
depend on Order in the middle of the saga, to learn something the message could simply carry.

**Rejected: leaving Payment currency-free and "knowing" it is the default.** That is precisely the
defect the spec opens with, preserved in the one table where money is recorded.

## D5 - Rounding to a currency's minor unit

**Decision**: the currency carries its number of decimal places - `USD` 2, `VND` **0** - and every
computed amount is rounded to it, halves away from zero, exactly as `OrderTotals` already rounds to 2.

`OrderTotals.Compute` gains a `decimals` parameter. Its default stays `2`, so its existing callers and
its existing tests are unchanged.

**What this affects**: tax per line, tax on delivery. Nothing else computes - unit prices and delivery
prices are entered at the currency's scale and multiplied by integers.

**Measured against the CHECK constraint**: the constraint on `orders` requires
`Subtotal + ShippingPrice + TaxTotal - DiscountTotal = TotalAmount`. Rounding each tax to 0 decimals
for VND and summing the rounded amounts keeps that identity exactly, because the total is *computed
from* the rounded parts and never re-derived. Per-line rounding differing from rounding the sum is
already accepted and recorded (specs/012 research D3); this only changes the unit it differs by.

**The storage column stays `decimal(18,2)`.** A VND amount rounded to 0 decimals stores as `x.00`
without loss, and narrowing the column would be a contracting migration that strands an earlier image
(constitution: expand then contract). It is recorded that the column is two decimals wider than VND
needs, and that this is harmless.

**Rejected: storing minor units as integers** (`4000000000` for 40,000,000.00). It is the textbook
answer and it is a rewrite of every money column, every DTO and every test in six services, to fix a
problem this project does not have.

## D6 - What the storefront stores, and how it formats

**Decision**: the chosen currency lives in `localStorage` under its own key, separate from the
language, and is sent as `X-Currency` on every request by the same axios interceptor that already
sends `Accept-Language`.

Formatting is `Intl.NumberFormat(language, { style: 'currency', currency })` - **two inputs, and they
are not the same input**. The language decides the separators and the symbol's position; the currency
decides the symbol and the number of decimals. `40.000.000 ₫` in Vietnamese and `₫40,000,000` in
English are the same amount, and both are correct for their reader.

Switching currency invalidates every cached query, for the same reason switching language does: every
cached answer was fetched in the old currency and a price is the thing most likely to be believed.

**The seam for a third currency**: `Intl` knows every currency's decimals already, so the storefront
needs no table. The server does need one, because it computes; it is three fields in configuration.

## D7 - The existing amounts are dong, and saying so is a decision (found while writing)

`products.Price` holds `40000000`; `Shipping:Options` holds `5.0`. The first is plainly dong and the
second is plainly not - it was written when neither meant anything.

**Decision**: the default currency is `VND`, the existing product and variant amounts are dong, and
**the delivery prices are corrected** to `30000` and `60000` dong with a dollar list beside them.
Product prices are left exactly as they are.

This is not cosmetic. Until now, every order ever placed added a forty-million-dong camera to a
five-dollar delivery charge and charged the sum. Nothing was wrong in any row, because no row claimed
a currency - which is the whole argument of this feature, and it is sitting in the repository's own
configuration file rather than in a hypothetical.

## D8 - Rounding what is computed is not enough (found while building)

An order placed in dong came back with tax of a whole **3,003** and a subtotal of **29.97**. The
rounding in D5 was working exactly as designed and was beside the point: a subtotal is a unit price
times an integer, so there is nothing in it to round. The fractional dong came from the *stored
price*, which had been entered as `9.99` when nothing in the system had an opinion about currencies.

**Decision**: every command that sets a price refuses an amount the currency cannot hold.
`Currency.Fits` is the one rule; `SetVariantPrice`, `CreateProduct`, `AddProductVariant` and
`UpdateProductVariant` all apply it. 9.99 is a price in dollars and is not one in dong.

**The cost, recorded**: rows written before this keep their amounts - validation is on writes, not on
reads, and rewriting somebody's stored prices to satisfy a new rule would be inventing data. The
seeded test products are the visible example: several are priced in fractional dong and will stay
that way until an administrator re-prices them.

**Why this was not in the plan**: it looked like D5 had covered it. Rounding the *computation* and
constraining the *input* are two different jobs, and only one of them was written down.

## D9 - The configuration binder appends to a defaulted array (found while building)

`AddRequestCurrency` refused to start with "Currency 'VND' is configured twice." The options class
declared `Supported = [VND, USD]` as a default and the configuration supplied the same two, and
.NET's binder **appends to a non-empty array rather than replacing it**.

`LanguageOptions` had the identical shape and had been running as `["vi", "en", "vi", "en"]` since
specs/021 - harmless there, because every read of that list is a `Contains` or a `FirstOrDefault`,
which is exactly why nobody noticed.

**Decision**: neither options type has a default any more. A service that wants languages or
currencies configures them; one that does not, does not start. The duplicate check stays, because it
is what found this.
