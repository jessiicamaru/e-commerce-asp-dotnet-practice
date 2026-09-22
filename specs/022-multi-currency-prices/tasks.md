# Tasks: Two Price Lists, Not One Price Converted

**Feature**: [spec.md](spec.md) · **Plan**: [plan.md](plan.md)

## Phase 1 - The currency itself (blocking; everything needs it)

- [X] T001 Add `Currency`, `CurrencyOptions` and `IRequestCurrency`/`RequestCurrency` in `server/src/BuildingBlocks/Ecommerce.Shared/Money/`
- [X] T002 Add `AddRequestCurrency()` + `UseRequestCurrency()` in `server/src/BuildingBlocks/Ecommerce.Shared/Money/DependencyInjection.cs`, refusing at startup a default currency that is not supported
- [X] T003 [P] Add the `Money` section to Catalog, Order and Payment `appsettings.json`

## Phase 2 - Catalog owns the price lists

- [X] T004 Add `VariantPrice` in `server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/VariantPrice.cs` and the navigation on `ProductVariant`
- [X] T005 Map it in `.../Infrastructure/Persistence/Configurations/`, unique on `(VariantId, Currency)` with a `CHECK (Amount >= 0)`
- [X] T006 Migration `AddVariantPrices`, seeding the USD list from the VND list at the rate stated in the migration
- [X] T007 `Priced` helper in `.../Application/Products/Common/`: the price of a variant in a currency, **null and never converted** when absent
- [X] T008 Make `price` nullable on the product and variant responses and add `currency`
- [X] T009 `ProductRepository` loads prices for the requested currency; the product's "from" price is the cheapest variant priced in it
- [X] T010 `PUT`/`DELETE /api/products/{id}/variants/{variantId}/prices/{currency}` — Admin, refusing DELETE of the default currency
- [X] T011 Wire `AddRequestCurrency` into Catalog `Program.cs`

## Phase 3 - Order prices a checkout in one currency

- [X] T012 `orders.Currency` + migration `AddOrderCurrency`
- [X] T013 `ShippingOption` carries a price per currency; `ConfiguredShippingOptions` reads `Prices` and refuses a startup where nothing is priced in the default currency
- [X] T014 `Shipping:Options` in `appsettings.json` corrected to dong, with a dollar list (research D7)
- [X] T015 `OrderTotals.Compute` gains `decimals`, defaulted to 2 so existing callers are unchanged
- [X] T016 `CheckoutPricing` resolves the currency, asks Catalog in it, refuses a variant not priced in it, and returns it on `PricedCheckout`
- [X] T017 `SubmitOrderCommandHandler` stores the currency; `GetShippingOptionsQuery` returns only options priced in it
- [X] T018 Order responses and the quote carry `currency`
- [X] T019 Wire `AddRequestCurrency` into Order `Program.cs`

## Phase 4 - The currency reaches the row that records the charge

> The step this project has already got wrong once, in the same relay (research D4).

- [X] T020 `OrderSubmittedEvent` and `ProcessPaymentCommand` gain `Currency`, additive and defaulted
- [X] T021 `OrderStateData.Currency`; the saga stores it and relays it into `ProcessPaymentCommand`
- [X] T022 `payments.Currency` + migration; the consumer stores what it was sent, reading `""` as the default
- [X] T023 Rebuild **every** image, the Orchestrator included, and verify against the running stack

## Phase 5 - gRPC

- [X] T024 `currency` on `PriceVariantsRequest`, `DescribeVariantsRequest` and `PricedVariant`
- [X] T025 `CatalogPricingService` prices in the requested currency; an unpriced variant answers `sellable: false` with an empty price
- [X] T026 Order's `ICatalogPrices` implementation passes the currency; Cart's `DescribeVariants` call passes it too

## Phase 6 - The storefront

- [X] T027 `client/src/config/money/` — the supported currencies, the default, the stored choice
- [X] T028 The axios interceptor sends `X-Currency` beside `Accept-Language`
- [X] T029 `money()` formats with `Intl.NumberFormat(language, { style: 'currency', currency })`
- [X] T030 A currency switcher beside the language switcher, invalidating every cached query on change
- [X] T031 Every price display handles `null` — "not sold in USD", and no add-to-cart
- [X] T032 Strings for both in `client/src/locales/{vi,en}/`

## Phase 7 - Evidence

- [X] T033 [P] `RequestCurrencyTests` — resolution order, the unsupported code, **and that the language does not decide it**
- [X] T034 [P] `VariantPriceTests` — the stored amount comes back exactly; a missing price is null and not sellable; the "from" price
- [X] T035 [P] `OrderCurrencyTests` — the order freezes it; a null currency reads as the default; VND has no fractional part and the parts still sum
- [X] T036 [P] `SagaCurrencyRelayTests` — the currency survives the relay into `ProcessPaymentCommand`
- [X] T037 Run all four negative controls from [quickstart.md](quickstart.md) and record what went red
- [X] T038 Bruno: a USD read, a price write, the unpriced refusal, the quote in both currencies
- [X] T039 `verify-saga.sh` in both currencies, and `verify-auth.sh` unchanged
- [X] T040 Update `CLAUDE.md` and the docs that state a price has no currency

## Dependencies

Phase 1 blocks everything. Phase 2 and Phase 3 can proceed in parallel after it, but Phase 3's
checkout cannot be tested end to end until Phase 5 carries the currency over gRPC. Phase 4 depends on
Phase 3. Phase 6 depends on Phases 2, 3 and 5. Phase 7 is last by definition.

---

## What building this found

Four things the plan did not anticipate. Each is recorded where it belongs as well as here.

1. **Rounding what is computed is not enough** (research D8). A dong order came back with tax of a
   whole 3,003 and a subtotal of **29.97**. A subtotal is a unit price times an integer, so there is
   nothing in it to round: the fraction came from a *stored* price entered when nothing had an
   opinion about currencies. Every command that sets a price now refuses an amount the currency
   cannot hold. **Found against the running stack, not by a test.**

2. **The configuration binder appends to a defaulted array** (research D9). `AddRequestCurrency`
   refused to start with "Currency 'VND' is configured twice", and the same shape meant
   `LanguageOptions.Supported` had been `["vi", "en", "vi", "en"]` since specs/021. Harmless there,
   which is why nobody had noticed. Neither options type has a default any more.

3. **Every refusal written for the customer was invisible outside Development.** `ProblemDetails`
   replaced "Not sold in USD: Sony A7 IV" with "An error occurred while processing your request", so
   FR-003 - the refusal must NAME what it refused - was not met in any deployed image. The four
   exception types this system maps deliberately now show their message; everything unmapped still
   hides it. **Found by a Bruno test asserting on the wording of a 409.**

4. **Cart had `AddRequestCurrency` and no `Money` configuration**, so it refused to start. That is
   the startup check doing its job - the alternative was a service that came up healthy and priced
   carts in a currency nobody configured.

And one thing that was promised and never delivered, found on the way past: specs/021's
`contracts/api.md` says the order responses gain `language`. They did not - the order stored it and
no response carried it. Both `language` and `currency` are on them now.

## What is deliberately not done

- **Category names have no currency and no translation.** Out of scope in specs/021 and still.
- **Stored prices written before this feature keep their amounts.** Validation is on writes; rewriting
  somebody's prices to satisfy a new rule would be inventing data. Several seeded test products are
  priced in fractional dong and will stay that way until an administrator re-prices them.
- **`decimal(18,2)` is two decimal places wider than dong needs.** Narrowing it would strand an
  earlier image (research D5).
- **Nobody has clicked through the storefront in a browser.** The currency switcher type-checks,
  lints and builds, and the server half is verified end to end. That is not the same thing.
