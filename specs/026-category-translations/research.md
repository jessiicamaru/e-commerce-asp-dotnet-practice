# Phase 0 Research: Category Translations

> Written on 2026-09-27, after the feature merged (#63), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-22

No separate design record was written while the feature was built; these decisions are reconstructed
from the code, its comments and the pull request. Every one of them repeats a decision
[specs/021](../021-internationalisation/research.md) already made for products.

---

## D1 - Do it now, although specs/021 put it out of scope

**Decision**: Translate category names and descriptions in their own feature, immediately after the
storefront redesign.

**Rationale**: specs/021 recorded category names as "the next thing to translate, and out of scope here so
the feature stays reviewable" - true at the time. The redesign (specs/025) put category names under every
product card and in the filter, and an English page full of `Máy ảnh không gương lật` became the most
visible flaw on it. "A gap that is recorded is not the same as a gap that is acceptable."

**Alternatives considered**:

- **Leave it recorded as a gap.** Rejected for the reason above.
- **Rename the categories in English.** Rejected: most of the shop's customers read Vietnamese, and the
  default-language text is Vietnamese.

---

## D2 - The same shape as `product_translations`

**Decision**: A `category_translations` table - `Id`, `CategoryId`, `Language` (`varchar(10)`), `Name`
(`varchar(100)`), `Description` (`varchar(500)`, nullable) - unique on `(CategoryId, Language)`, cascading
from the category. The category's own `Name` and `Description` stay as the default-language text.

**Rationale**: One pattern for all translated text means one set of rules to learn and nothing new to get
wrong. Keeping the category's own columns as the default language makes the migration additive: an
untranslated category still reads, and an earlier Catalog image, which knows nothing of the table, still
runs. A language is a tag, not an enum, so a third language is rows. The unique index is there because
two Vietnamese names would make the answer depend on which row a query read first. The name and
description lengths match the category's own columns' validation (100 and 500).

**Alternatives considered**:

- **JSON column of names per language on `categories`.** Rejected: a different pattern from products for
  the same job, and no uniqueness per language in the database.
- **Move the Vietnamese text into the new table too.** Rejected: a data migration and a breaking change
  for no behavioural gain; the columns already are the default.

---

## D3 - Per-field fallback, and say which language came back

**Decision**: `CategoryResponse.From` takes each field from the translation when there is one and from the
category otherwise, and sets `Language` to the requested language when a translation exists, to the
default language when it does not, and to empty when no language was asked for.

**Rationale**: Per field, like a product, so a translated name with no translated description still shows
the half somebody got to. The `language` field is how a client tells "this is English" from "nobody has
written the English yet" - the Bruno fallback read asserts it. `The_fallback_is_per_field_like_a_products`
and `An_untranslated_category_shows_its_own_text_and_says_it_is_the_default` hold both rules.

**Alternatives considered**:

- **All-or-nothing fallback.** Rejected: it hides a partial translation.
- **No `language` field.** Rejected: a client cannot tell a fallback from a translation.

---

## D4 - An upsert to write, a quiet no-op to remove what is not there

**Decision**: `PUT /api/categories/{id}/translations/{lang}` creates or replaces; `DELETE` removes and
answers 204 even when there was nothing to remove. Both are Admin only, and both refuse a language the
shop does not speak through `MustBeSupported` (400). The language in the path is lower-cased.

**Rationale**: A translation is a fact about a category in a language rather than an event, so the second
attempt at the English name should simply be the English name
(`Writing_a_translation_twice_replaces_it_rather_than_conflicting`). Removing what is not there leaves the
category exactly as asked - showing its default text - so it is not an error. A row in a language nobody
reads "is a lie about what the shop offers"
(`A_language_the_shop_does_not_speak_is_refused_rather_than_stored`).

**Alternatives considered**:

- **POST to create, 409 on a second.** Rejected: products use an upsert, and a conflict here protects
  nothing.

---

## D5 - Load translations in both repository reads

**Decision**: `CategoryRepository.GetByIdAsync` and `GetAllAsync` both `Include(c => c.Translations)`.

**Rationale**: The write path reads through `GetByIdAsync`, and an upsert that cannot see the existing rows
inserts a second row per language - which the unique index would then refuse. The list is what the
storefront reads; without the include every category on every page falls back to its default text, which
is exactly what the storefront was showing.

**Alternatives considered**:

- **Project the one wanted translation in SQL.** Rejected as unnecessary: the real catalogue has two
  categories. Not recorded as measured.
