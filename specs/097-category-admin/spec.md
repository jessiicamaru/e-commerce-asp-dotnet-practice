# Feature Specification: Administrators manage categories

**Feature Branch**: `097-category-admin` | **Created**: 2026-09-27 | **Issue**: #195

**Status**: Merged (#204, 2026-09-27)

**Input**: Issue #195 - "administrators have no screen to manage categories".

## Why

Categories can be created, deleted and translated only by calling the API: the storefront has no page for them, so an
administrator opening a new line of goods uses Bruno or curl. And a category's own name and description - its
default-language text (Vietnamese, `Localization:DefaultLanguage`) - cannot be changed at all once created; only its
translations can (specs/026).

Reading the code for this found two more faults in the same place:

- **A second category with a slug already taken is a 500**, not a 409: `CreateCategoryCommandHandler` throws a bare
  `Exception("Category already exists")`, which the shared handler cannot map - the #28 class of defect.
- **`categories.IsActive` is dead data**: every category created through the API is `false` (the entity's default), all
  19 in the running database are `false`, and nothing reads it. It is left alone here (Out of scope).

## User Scenarios & Testing *(mandatory)*

### US1 - An administrator lists, creates and deletes categories from the storefront (Priority: P1)

`/admin/categories` lists every category with its Vietnamese and English name and how it reads in each language; a form
creates one (the address - slug - suggested from the name); deleting one with products in it is refused and the page says
why (specs/024's 409).

**Why this priority**: The missing screen is the issue.

**Independent Test**: As an administrator, create "Ống kính" with slug `ong-kinh`, see it listed, delete it.

**Acceptance Scenarios**:

1. **Given** the page, **Then** every category is listed with both languages' names.
2. **Given** a name, **When** the administrator creates the category, **Then** it is listed; the slug defaults to the
   name without diacritics, lower-case, hyphenated.
3. **Given** a slug already taken, **When** creating, **Then** 409 in words, and the page shows it.
4. **Given** a category with products, **When** deleting, **Then** 409 "has products" in words; an empty one is deleted.

---

### US2 - An administrator renames a category (Priority: P1)

`PUT /api/categories/{id}` changes the default-language name and description; the slug stays. The page edits both
languages side by side: Vietnamese through this endpoint, English through the existing translation endpoint (specs/026).

**Why this priority**: A typo in a category name was permanent.

**Independent Test**: Rename a category; the public list shows the new name in Vietnamese; its English translation is
untouched.

**Acceptance Scenarios**:

1. **Given** a category, **When** an administrator renames it, **Then** the public list shows the new name, the slug is
   unchanged, and the change is audited (before and after).
2. **Given** an unknown id, **Then** 404. **Given** an empty name, **Then** 400.
3. **Given** the English fields, **When** saved, **Then** the English translation is set; cleared, it is removed and the
   category falls back to its Vietnamese text.

---

### US3 - Only administrators (Priority: P1)

Creating, renaming, translating and deleting are Admin; listing stays public.

**Acceptance Scenarios**:

1. **Given** a moderator, seller or customer token, **When** renaming, **Then** 403.

### Edge Cases

- **Renaming does not change the slug.** Links to `/?category=` and saved searches keep working (Decision).
- **Two administrators renaming at once.** Last write wins; both are audited. Not guarded - a name is not money.
- **A translation equal to the default text.** Allowed; it is the administrator's choice.
- **A category that is somebody's parent.** Deleting it: unchanged behaviour (specs/024 checks products only).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `PUT /api/categories/{id}` (Admin) with `{ name, description }` updates the default-language text, audited
  as `CategoryUpdated` with before and after; 404 for an unknown id; the validator of creation (name 1-100, description
  ≤ 500).
- **FR-002**: Creating a category whose slug is taken is `ConflictException` (409), not a 500.
- **FR-003**: `/admin/categories` (Admin only, in the menu) lists, creates, renames, translates (English) and deletes.
- **FR-004**: The slug is never changed after creation.
- **FR-005**: No migration; `IsActive` is not given a meaning here.

### Key Entities

- **Category** - `Name`, `Description` (default language), `Slug`, translations (specs/026).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Catalog tests: rename (and the slug kept, the audit written, 404, 400), a duplicate slug is 409.
- **SC-002**: Storefront tests: the list shows both languages; create sends the suggested slug; a rename and an English
  translation are sent to their endpoints; a refusal is shown in its words.
- **SC-003**: Bruno: rename is 200 for an administrator and 403 for a customer.
- **SC-004**: Mutations - the rename not saved, a duplicate slug back to a bare exception - each red.

## Decision

1. **The slug is fixed at creation.** A rename that changed it would break every link to the category; one that kept an
   old slug under a new name is a small inconsistency nobody sees. Recorded as decided on the user's behalf
   ([research.md](research.md) D1).
2. **`IsActive` stays out.** Giving it meaning needs a data fix (all rows are `false`) and a rule for products under an
   inactive category; nothing asks for it yet (D3).

## Assumptions

- Vietnamese is the default language and English the one other (`Localization:Supported`), as today.

## Out of scope

- Nested categories in the UI (`ParentCategoryId` is kept as is).
- Deactivating categories.
