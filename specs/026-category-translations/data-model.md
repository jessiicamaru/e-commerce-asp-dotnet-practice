# Phase 1 Data Model: Category Translations

> Written on 2026-09-27, after the feature merged (#63), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

One new table in `ecommerce_catalog_db`, added by migration `20260922171155_AddCategoryTranslations`.
Nothing existing changed.

---

## `category_translations`

A category's name and description in one language. Mapped by `CategoryTranslationConfiguration` in
`Configurations/TranslationConfigurations.cs`, beside the product and option translations.

| Column | Type | Constraints | Meaning |
| :--- | :--- | :--- | :--- |
| `Id` | `uuid` | PK `PK_category_translations`; `ValueGeneratedNever()` - the handler sets `Guid.CreateVersion7()` | |
| `CategoryId` | `uuid` | Not null; FK `FK_category_translations_categories_CategoryId` → `categories."Id"`, `ON DELETE CASCADE` | The category translated |
| `Language` | `character varying(10)` | Not null | A language tag, lower-cased by the handler: `vi`, `en` |
| `Name` | `character varying(100)` | Not null | The name in that language |
| `Description` | `character varying(500)` | Nullable | The description in that language; null falls back to the category's own |

**Indexes**: unique `IX_category_translations_CategoryId_Language` on `("CategoryId", "Language")` - one text
per language per category.

**Why `ValueGeneratedNever()`**: the translation is added through the category's `Translations`
collection with its id already set; by convention EF would take a set `Guid` key for an existing row and
issue an `UPDATE` that affects nothing (the CLAUDE.md gotcha).

---

## `categories` - unchanged

`Name` (100) and `Description` (500) keep holding the **default-language** (Vietnamese) text and are the
per-field fallback. `Category` gained a `Translations` navigation only.

---

## Reading

```text
name(category, lang)        = translation(category, lang)?.Name        ?? category.Name
description(category, lang) = translation(category, lang)?.Description ?? category.Description
language(category, lang)    = lang empty        -> ""
                              translation exists -> lang
                              otherwise          -> the default language (Localization:DefaultLanguage)
```

---

## Migration and older images

`Up` creates the table, its foreign key and its unique index; `Down` drops the table. It is **additive**
under the constitution's schema-evolution rule: an earlier Catalog image never reads the table and runs
unchanged. Deleting a category (specs/025) takes its translations with it through the cascade.
