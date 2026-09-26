# Tasks: Speaking More Than One Language

## Phase 1: Knowing which language was asked for

- [X] T001 `Ecommerce.Shared/Localization/`: `IRequestLanguage`, `RequestLanguage` (query `?lang=` → `Accept-Language` → default), `LanguageOptions`, `AddRequestLanguage()`
- [X] T002 Catalog and Order call it in `Program.cs`; `Localization:DefaultLanguage`/`Supported` in both `appsettings.json`
- [X] T003 Tests: the precedence, a loose match (`vi-VN` → `vi`), an unsupported tag falling back

## Phase 2: Catalog stores and serves translations (US2)

- [X] T004 `ProductTranslation` and `VariantOptionTranslation` entities + EF configurations (unique `(owner, Language)`, cascade)
- [X] T005 Migration `AddTranslations`, including `CREATE EXTENSION IF NOT EXISTS unaccent`
- [X] T006 Repository reads: include translations; `ProductResponse`/`VariantResponse` built **for a language**, falling back to the default text, and carrying `language`
- [X] T007 `PUT/DELETE /api/products/{id}/translations/{lang}` and the option translation endpoint, Admin only
- [X] T008 Search matches the requested language and the default text, ignoring diacritics (research D5)
- [X] T009 Catalog tests: fallback per field, a Vietnamese read, `may anh` finding `máy ảnh`, an unsupported language, translations deleted with their product

## Phase 3: The words an order keeps (US3)

- [X] T010 `language` on `PriceVariantsRequest`/`DescribeVariantsRequest`; Catalog answers in it
- [X] T011 `orders.Language` + migration; checkout passes the request's language to Catalog and stores it
- [X] T012 Order tests: an order placed in Vietnamese freezes Vietnamese words and still reads them after the catalogue changes; an order placed before this feature is unaffected

## Phase 4: The interface speaks (US1, US4)

- [X] T013 `react-i18next` wired in `config/i18n`, `vi` and `en` JSON under `locales/`, namespaces per area
- [X] T014 The chosen language is stored and sent as `Accept-Language` by the axios instance
- [X] T015 A switcher in the top bar
- [X] T016 Every visible string through `useTranslation`, including the error sentences the client already words (research D4)
- [X] T017 Money and dates formatted for the chosen language (US4)

## Phase 5: Evidence

- [X] T018 Bruno: a Vietnamese product read, a translation written, the diacritic search, the Admin refusals
- [X] T019 Negative controls: drop the fallback; ignore the language on the gRPC call; search only the default text
- [X] T020 Through the gateway on the containerised stack, in both languages
- [X] T021 Docs: CLAUDE.md, client/README.md, the spec's status
- [X] T022 PR; CI green; squash-merge — *Merged as #58; every check green (success, publish skipped on the PR). Ticked on 2026-09-27 from the PR's record.*

## Dependencies

Phase 1 before everything. Phase 2 before 3 (the order freezes what Catalog says). Phase 4 is
independent of 2 and 3 and can be done in parallel by a second person.

## What actually happened

- **203 tests pass** (Catalog 48, Order 52, Identity 50, Inventory 26, Cart 14, Payment 13). New:
  `LanguageNegotiationTests` (the middleware against real requests), `TranslationTests`,
  `OrderLanguageTests`.
- Negative controls, each restored: drop the fallback → 3 fail; search the default text only, with
  diacritics → 1 fails; checkout ignores the language → 2 fail.
- **Bruno 73/73, 103 tests**, including a Vietnamese read, the English fallback, the accent-free
  search, and the two refusals.
- `verify-saga.sh` and `verify-auth.sh` pass unchanged.
- Through the gateway, on the containerised stack:

  ```text
  PUT …/translations/vi           -> 200
  PUT …/translations/fr           -> 400   (a language the shop does not speak)
  GET …?lang=vi                   -> vi | Máy ảnh Sony A7 IV | Bộ: Chỉ thân máy
  GET … Accept-Language: en-GB    -> vi | Sony A7 IV | Kit: Body only    (falls back, and says so)
  GET … en;q=0.4,vi;q=0.9         -> Vietnamese, because q-values decide
  searchTerm=may anh              -> 1: Máy ảnh Sony A7 IV
  order placed in vi, read in en  -> Máy ảnh Sony A7 IV · Bộ: Chỉ thân máy   (frozen)
  ```

- **Two things this turned up.** The hand-rolled `Accept-Language` parser was wrong for
  `en;q=0.4,vi;q=0.9` and the responses had no `Vary`, so a cache could cross two shoppers over; both
  are fixed by using the framework's negotiation. And the **cart** showed English while the product
  page showed Vietnamese, because Cart asked Catalog without a language - found by reading a cart, not
  by a test.
