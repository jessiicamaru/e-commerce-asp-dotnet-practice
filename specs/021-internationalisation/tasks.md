# Tasks: Speaking More Than One Language

## Phase 1: Knowing which language was asked for

- [ ] T001 `Ecommerce.Shared/Localization/`: `IRequestLanguage`, `RequestLanguage` (query `?lang=` → `Accept-Language` → default), `LanguageOptions`, `AddRequestLanguage()`
- [ ] T002 Catalog and Order call it in `Program.cs`; `Localization:DefaultLanguage`/`Supported` in both `appsettings.json`
- [ ] T003 Tests: the precedence, a loose match (`vi-VN` → `vi`), an unsupported tag falling back

## Phase 2: Catalog stores and serves translations (US2)

- [ ] T004 `ProductTranslation` and `VariantOptionTranslation` entities + EF configurations (unique `(owner, Language)`, cascade)
- [ ] T005 Migration `AddTranslations`, including `CREATE EXTENSION IF NOT EXISTS unaccent`
- [ ] T006 Repository reads: include translations; `ProductResponse`/`VariantResponse` built **for a language**, falling back to the default text, and carrying `language`
- [ ] T007 `PUT/DELETE /api/products/{id}/translations/{lang}` and the option translation endpoint, Admin only
- [ ] T008 Search matches the requested language and the default text, ignoring diacritics (research D5)
- [ ] T009 Catalog tests: fallback per field, a Vietnamese read, `may anh` finding `máy ảnh`, an unsupported language, translations deleted with their product

## Phase 3: The words an order keeps (US3)

- [ ] T010 `language` on `PriceVariantsRequest`/`DescribeVariantsRequest`; Catalog answers in it
- [ ] T011 `orders.Language` + migration; checkout passes the request's language to Catalog and stores it
- [ ] T012 Order tests: an order placed in Vietnamese freezes Vietnamese words and still reads them after the catalogue changes; an order placed before this feature is unaffected

## Phase 4: The interface speaks (US1, US4)

- [ ] T013 `react-i18next` wired in `config/i18n`, `vi` and `en` JSON under `locales/`, namespaces per area
- [ ] T014 The chosen language is stored and sent as `Accept-Language` by the axios instance
- [ ] T015 A switcher in the top bar
- [ ] T016 Every visible string through `useTranslation`, including the error sentences the client already words (research D4)
- [ ] T017 Money and dates formatted for the chosen language (US4)

## Phase 5: Evidence

- [ ] T018 Bruno: a Vietnamese product read, a translation written, the diacritic search, the Admin refusals
- [ ] T019 Negative controls: drop the fallback; ignore the language on the gRPC call; search only the default text
- [ ] T020 Through the gateway on the containerised stack, in both languages
- [ ] T021 Docs: CLAUDE.md, client/README.md, the spec's status
- [ ] T022 PR; CI green; squash-merge

## Dependencies

Phase 1 before everything. Phase 2 before 3 (the order freezes what Catalog says). Phase 4 is
independent of 2 and 3 and can be done in parallel by a second person.
