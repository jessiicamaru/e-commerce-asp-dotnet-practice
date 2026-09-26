# Feature Specification: Category Translations

> Written on 2026-09-27, after the feature merged (#63), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature Branch**: `026-category-translations`

**Created**: 2026-09-22

**Status**: Merged as [#63](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/63) on 2026-09-22

**Input**: specs/021 recorded category names as out of scope twice, on the grounds that the feature stayed
reviewable. The storefront redesign (specs/025) made that gap the most visible flaw on the page: reading
the shop in English, every product card said `Máy ảnh không gương lật` under its name, and the filter
offered the same.

## Context

"A gap that is recorded is not the same as a gap that is acceptable." Products, their descriptions and
their option names already spoke Vietnamese and English (specs/021); the categories they are filed under
did not. This feature gives categories the same treatment, in the same shape, so nothing about it is new
except the table.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A shopper reading English sees English category names (Priority: P1)

A shopper who has chosen English sees category names and descriptions in English, on product cards and
in the filter.

**Why this priority**: This is the defect that prompted the feature; everything else supports it.

**Independent Test**: Give a category an English name, then request `GET /api/categories` with
`Accept-Language: en` and with `vi`.

**Acceptance Scenarios**:

1. **Given** a category with an English translation, **When** the categories are read in English,
   **Then** it shows the English name and description and reports `language: "en"`.
2. **Given** the same category, **When** read in Vietnamese, **Then** it shows the text it was created with
   and reports `language: "vi"`.

---

### User Story 2 - An untranslated category still reads (Priority: P1)

A category nobody has translated shows the text it was created with, and says so.

**Why this priority**: Without it, adding a language would blank every category until someone typed
every name.

**Independent Test**: Read an untranslated category in English.

**Acceptance Scenarios**:

1. **Given** a category with no English translation, **When** read in English, **Then** it shows its own
   name and description and reports the default language, which is how a client tells "this is English"
   from "nobody has written the English yet".
2. **Given** a translation with a name and no description, **When** read in that language, **Then** the
   name is translated and the description falls back to the category's own - per field, like a product's.

---

### User Story 3 - An administrator writes and removes a category's translation (Priority: P2)

An administrator sets a category's name and description in a language, corrects it by setting it again,
and can remove it so the category falls back to its own text.

**Why this priority**: The means of producing User Story 1's result. The seeder uses it for the real
categories.

**Independent Test**: `PUT` an English translation twice with different text, then `DELETE` it, reading
the category after each.

**Acceptance Scenarios**:

1. **Given** a category, **When** an administrator puts an English translation, **Then** the answer is 200
   with the category in English.
2. **Given** an English translation exists, **When** another is put, **Then** it replaces the first rather
   than conflicting or adding a second.
3. **Given** an English translation, **When** it is deleted, **Then** the category reads its own text again.
4. **Given** a language the shop does not speak, **When** a translation is put in it, **Then** it is refused
   (400) rather than stored.
5. **Given** no category has the id, **When** a translation is put, **Then** the answer is 404.

---

### User Story 4 - The seeded shop reads correctly in both languages (Priority: P3)

Running the seeder gives the two real categories their English names, with nobody typing them.

**Why this priority**: Convenience for every developer's catalogue; the API is the feature.

**Independent Test**: Run `seed/seed-catalogue.py` and read the categories in English.

**Acceptance Scenarios**:

1. **Given** a seeded catalogue, **When** the categories are read in English, **Then** they read "Mirrorless
   cameras" and "Compact cameras".

---

### Edge Cases

- **Deleting a translation that is not there.** Not an error (204): the category already shows its
  default text.
- **A category deleted with its translations.** They go with it (cascade).
- **Language case.** `EN` in the path is stored as `en`.
- **Two translations in one language.** Impossible: one row per category per language, or the answer
  would depend on which row a query read first.
- **A request with no language.** Over HTTP there is always one (`?lang=`, then `Accept-Language`, then the
  default); a handler called without one leaves `language` empty.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST store a category's name and description per supported language, at most one
  per language.
- **FR-002**: A category read in a language MUST use that language's text where it exists, and the
  category's own text for each field that does not - per field.
- **FR-003**: A category read MUST say which language its text is in after the fallback.
- **FR-004**: An administrator MUST be able to set (create or replace) and remove a category's translation.
- **FR-005**: A translation in a language the shop does not speak MUST be refused.
- **FR-006**: The category's own text MUST remain the default-language text, so an untranslated category
  still reads and the change is additive.
- **FR-007**: Deleting a category MUST delete its translations.
- **FR-008**: The seeder MUST write the English names of the real categories.

### Key Entities

- **Category translation**: a category's name and description in one language. Many per category, one per
  language.
- **Category**: unchanged; its own name and description are the default-language text and the fallback.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a seeded catalogue read in English, no category name is Vietnamese. The PR read back:
  `en  Mirrorless cameras [en]` and `vi  Máy ảnh không gương lật [vi]`.
- **SC-002**: An untranslated category never reads blank in any supported language.
- **SC-003**: Every test project passes with the rules above covered (262 at the merge, 7 new) and Bruno's
  fallback read would catch a response reporting the wrong language (87/87 requests, 130 tests).

## Assumptions

- The rules are exactly those of product text in specs/021; nothing about fallback or negotiation is
  redesigned.
- Only administrators manage categories, so only administrators translate them.
- Vietnamese is the default language and the one categories were created in.

## Out of Scope

- A storefront screen for translating categories (the API and the seeder are the means).
- Translating slugs: a slug is an identifier, like a SKU.
- Searching products by category name.
