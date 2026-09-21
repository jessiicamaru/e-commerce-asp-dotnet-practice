# ADR 002: Catalogue Prices Exclude Tax, and How Tax Is Rounded

* **Status**: Accepted
* **Deciders**: project maintainer (the recommended option, taken on the owner's standing instruction)
* **Date**: 2026-09-22
* **Feature**: [specs/012-order-totals](../../specs/012-order-totals/)

---

## 1. Context

Until feature 012 an order had one number: the goods plus delivery. There was no tax, and nothing on
the order said how the number was reached — yet that number is exactly what payment takes.

Adding tax forces one decision everything else inherits: **does a price in the catalogue include
tax, or is tax added on top?** It changes what every existing `Price` means, so it is written down
here rather than left to be inferred from code.

## 2. Decision

**Prices exclude tax.** Tax is computed at checkout and shown as its own figure:

```text
total = subtotal + delivery + tax − discount
```

- **Rate**: from the destination country, configured in Order (`Tax:Rates:{ISO}`), falling back to
  `Tax:DefaultRate`. The rate applied is **stored on the order**.
- **Scope**: tax applies to each line and to delivery.
- **Rounding**: each line's tax, and delivery's tax, is rounded to 2 decimals with halves **away from
  zero** (`MidpointRounding.AwayFromZero`), then summed. Not .NET's default banker's rounding.
- **Discount**: a stored part, always 0 until a discount feature exists.

The database enforces the equation above with a CHECK constraint.

## 3. Alternatives Considered

| | Prices **exclude** tax (chosen) | Prices **include** tax |
| :--- | :--- | :--- |
| Existing catalogue prices | keep their meaning | silently become gross |
| Tax varying by destination | visible in the total | the merchant's net varies instead; the shopper's total does not |
| The parts of a total | sum directly | tax is a component *of* the subtotal ("of which VAT") |
| Consumer display rules (EU, Vietnam) | a shop selling to consumers there would need gross display added | native |

Inclusive pricing is the norm for consumer shops in the EU and Vietnam, and is the right answer for
a shop whose legal obligations require it. It was rejected **here** because it would reinterpret every
price already stored, and because this project has no such obligation yet. Moving to it later is a
display change plus extracting tax from gross prices — the stored parts and the rate already carry
what that needs.

**Rounding**, alternatives:

- *Round the sum once* — minimises rounding error, but a line's tax is then not a figure anyone can
  point to, and per-line tax is what receipts show.
- *Banker's rounding* (the .NET default) — statistically unbiased, and surprising to anyone checking
  a receipt by hand: 0.025 becomes 0.02.

Per-line rounding differs from rounding the sum by up to half a cent per line. That difference is
**accepted and not corrected**: the per-line tax stored is what was charged.

## 4. Consequences

- A rate change never alters an existing order — the rate and every part are frozen at checkout.
- Orders placed before feature 012 were given the parts that describe what they were actually
  charged: tax 0, rate 0.
- Payment charges `TotalAmount`, the stored grand total; no message contract changed.
- Tested: the half-cent case (`OrderTotalsTests`), and a row whose parts do not sum being refused by
  the database (`TotalsPersistenceTests`). Replacing `AwayFromZero` with `ToEven` fails two tests.
