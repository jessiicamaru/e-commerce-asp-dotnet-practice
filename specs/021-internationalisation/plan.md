# Implementation Plan: Speaking More Than One Language

**Branch**: `021-internationalisation` | **Date**: 2026-09-22 | **Spec**: [spec.md](spec.md)

## Summary

Two halves, as the spec insists on separating them:

- **The interface**: `react-i18next`, Vietnamese and English, a switcher, a stored choice, and an
  `Accept-Language` header on every call so the server's half agrees with it.
- **The content**: Catalog stores a translation per language for a product's name and description and
  for each option's name and value, falls back to the default-language text, and searches both -
  without diacritics. Order freezes the words **in the language the order was placed in**.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript 6 + React 19
**Primary Dependencies**: EF Core + Npgsql (`unaccent`), MediatR, Grpc.AspNetCore, react-i18next
**Storage**: two new Catalog tables, one nullable Order column. Additive throughout
**Testing**: xUnit against real PostgreSQL (Catalog, Order); Bruno; the storefront type-checks and builds
**Constraints**: an earlier image of every service keeps running; nothing that exists loses its text
**Scope**: Catalog, Order, `Ecommerce.Shared`, one proto, the storefront

## Constitution Check

| Principle | Check | Result |
| :-- | :-- | :-- |
| I. Service autonomy | Catalog owns product text; Order owns what it froze; neither reaches into the other. The language travels as a request value, not as shared state. | Pass |
| II. Clean Architecture | Translations are entities in Catalog.Domain, use cases in Application, EF mapping in Infrastructure. `IRequestLanguage` is an abstraction in `Ecommerce.Shared`, implemented over `HttpContext` there, exactly as `ICurrentUser` is. | Pass |
| III. Atomic writes, idempotent messaging | No new publisher, no new consumer. A translation write is one row, upserted. | Pass |
| IV. Identity from the token | Unchanged. Writing a translation is an Admin action by role. | Pass |
| V. Evidence over assumption | Negative controls per half; the diacritic search is measured, not assumed. | Pass |
| Schema evolution | Two new tables, one nullable column, no backfill needed. Default-language columns stay. | Pass |
| Invariants in the database | Unique `(owner, Language)` per translation; FKs cascade from the thing translated. | Pass |

No violations, so Complexity Tracking is empty. One thing is **recorded rather than solved**: the
diacritic-insensitive search has no index (research D5).

## Project Structure

```text
server/src/BuildingBlocks/
  Ecommerce.Shared/Localization/            new: IRequestLanguage, RequestLanguage, LanguageOptions,
                                            AddRequestLanguage() + UseRequestLanguage()
  Ecommerce.Contracts.Grpc/Protos/catalog_pricing.proto   + language on both requests

server/src/Services/Catalog/
  Domain/Entities/ProductTranslation.cs, VariantOptionTranslation.cs
  Application/Products/Translations/        new: upsert + remove, per product and per option
  Application/Products/Common/              responses carry `language`; mapping takes the language
  Infrastructure/Migrations/AddTranslations (+ CREATE EXTENSION unaccent)
  Infrastructure/.../ProductRepository      reads translations, searches both texts unaccented
  WebApi/Controllers/ProductsController     translation endpoints
  WebApi/Grpc/CatalogPricingService         answers in the requested language

server/src/Services/Order/                  orders.Language; checkout passes the language to Catalog
client/src/
  config/i18n/, locales/vi|en/*.json, components/layout/language-switcher/
  every page and component: strings through useTranslation
bruno/                                      a Vietnamese read, a translation write, the search
```

## Order of work

1. `Ecommerce.Shared` language resolution — everything else needs to know the language.
2. Catalog: tables, fallback reads, admin writes, search, gRPC.
3. Order: the column, and the language passed at checkout.
4. The storefront: i18next, the switcher, the strings.
5. Evidence: tests, negative controls, Bruno, the stack.

## Design artifacts

[research.md](research.md) · [data-model.md](data-model.md) · [contracts/api.md](contracts/api.md) ·
[quickstart.md](quickstart.md)
