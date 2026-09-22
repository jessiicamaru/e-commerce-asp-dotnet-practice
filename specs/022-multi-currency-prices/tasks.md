# Tasks: Two Price Lists, Not One Price Converted

**Feature**: [spec.md](spec.md) · **Plan**: [plan.md](plan.md)

## Phase 1 - The currency itself (blocking; everything needs it)

- [ ] T001 Add `Currency`, `CurrencyOptions` and `IRequestCurrency`/`RequestCurrency` in `server/src/BuildingBlocks/Ecommerce.Shared/Money/`
- [ ] T002 Add `AddRequestCurrency()` + `UseRequestCurrency()` in `server/src/BuildingBlocks/Ecommerce.Shared/Money/DependencyInjection.cs`, refusing at startup a default currency that is not supported
- [ ] T003 [P] Add the `Money` section to Catalog, Order and Payment `appsettings.json`

## Phase 2 - Catalog owns the price lists

- [ ] T004 Add `VariantPrice` in `server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/VariantPrice.cs` and the navigation on `ProductVariant`
- [ ] T005 Map it in `.../Infrastructure/Persistence/Configurations/`, unique on `(VariantId, Currency)` with a `CHECK (Amount >= 0)`
- [ ] T006 Migration `AddVariantPrices`, seeding the USD list from the VND list at the rate stated in the migration
- [ ] T007 `Priced` helper in `.../Application/Products/Common/`: the price of a variant in a currency, **null and never converted** when absent
- [ ] T008 Make `price` nullable on the product and variant responses and add `currency`
- [ ] T009 `ProductRepository` loads prices for the requested currency; the product's "from" price is the cheapest variant priced in it
- [ ] T010 `PUT`/`DELETE /api/products/{id}/variants/{variantId}/prices/{currency}` — Admin, refusing DELETE of the default currency
- [ ] T011 Wire `AddRequestCurrency` into Catalog `Program.cs`

## Phase 3 - Order prices a checkout in one currency

- [ ] T012 `orders.Currency` + migration `AddOrderCurrency`
- [ ] T013 `ShippingOption` carries a price per currency; `ConfiguredShippingOptions` reads `Prices` and refuses a startup where nothing is priced in the default currency
- [ ] T014 `Shipping:Options` in `appsettings.json` corrected to dong, with a dollar list (research D7)
- [ ] T015 `OrderTotals.Compute` gains `decimals`, defaulted to 2 so existing callers are unchanged
- [ ] T016 `CheckoutPricing` resolves the currency, asks Catalog in it, refuses a variant not priced in it, and returns it on `PricedCheckout`
- [ ] T017 `SubmitOrderCommandHandler` stores the currency; `GetShippingOptionsQuery` returns only options priced in it
- [ ] T018 Order responses and the quote carry `currency`
- [ ] T019 Wire `AddRequestCurrency` into Order `Program.cs`

## Phase 4 - The currency reaches the row that records the charge

> The step this project has already got wrong once, in the same relay (research D4).

- [ ] T020 `OrderSubmittedEvent` and `ProcessPaymentCommand` gain `Currency`, additive and defaulted
- [ ] T021 `OrderStateData.Currency`; the saga stores it and relays it into `ProcessPaymentCommand`
- [ ] T022 `payments.Currency` + migration; the consumer stores what it was sent, reading `""` as the default
- [ ] T023 Rebuild **every** image, the Orchestrator included, and verify against the running stack

## Phase 5 - gRPC

- [ ] T024 `currency` on `PriceVariantsRequest`, `DescribeVariantsRequest` and `PricedVariant`
- [ ] T025 `CatalogPricingService` prices in the requested currency; an unpriced variant answers `sellable: false` with an empty price
- [ ] T026 Order's `ICatalogPrices` implementation passes the currency; Cart's `DescribeVariants` call passes it too

## Phase 6 - The storefront

- [ ] T027 `client/src/config/money/` — the supported currencies, the default, the stored choice
- [ ] T028 The axios interceptor sends `X-Currency` beside `Accept-Language`
- [ ] T029 `money()` formats with `Intl.NumberFormat(language, { style: 'currency', currency })`
- [ ] T030 A currency switcher beside the language switcher, invalidating every cached query on change
- [ ] T031 Every price display handles `null` — "not sold in USD", and no add-to-cart
- [ ] T032 Strings for both in `client/src/locales/{vi,en}/`

## Phase 7 - Evidence

- [ ] T033 [P] `RequestCurrencyTests` — resolution order, the unsupported code, **and that the language does not decide it**
- [ ] T034 [P] `VariantPriceTests` — the stored amount comes back exactly; a missing price is null and not sellable; the "from" price
- [ ] T035 [P] `OrderCurrencyTests` — the order freezes it; a null currency reads as the default; VND has no fractional part and the parts still sum
- [ ] T036 [P] `SagaCurrencyRelayTests` — the currency survives the relay into `ProcessPaymentCommand`
- [ ] T037 Run all four negative controls from [quickstart.md](quickstart.md) and record what went red
- [ ] T038 Bruno: a USD read, a price write, the unpriced refusal, the quote in both currencies
- [ ] T039 `verify-saga.sh` in both currencies, and `verify-auth.sh` unchanged
- [ ] T040 Update `CLAUDE.md` and the docs that state a price has no currency

## Dependencies

Phase 1 blocks everything. Phase 2 and Phase 3 can proceed in parallel after it, but Phase 3's
checkout cannot be tested end to end until Phase 5 carries the currency over gRPC. Phase 4 depends on
Phase 3. Phase 6 depends on Phases 2, 3 and 5. Phase 7 is last by definition.
