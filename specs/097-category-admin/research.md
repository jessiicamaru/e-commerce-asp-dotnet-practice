# Research: Administrators manage categories

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #195

---

## D1 - The slug is fixed at creation

**Decision**: `PUT /api/categories/{id}` takes a name and description only.

**Rationale**: The slug is the category's address (`/?category=`, shared links). Changing it with the name breaks every
link; keeping it under a new name is a mismatch nobody reads. The page suggests a slug from the name when creating
(`slugOf`: accents and `đ` off, as `fold` does for search), and the administrator may edit it before saving.

**Alternatives considered**: a slug editable later with a redirect table - rejected as machinery for a rare need.

---

## D2 - English through the existing translation endpoint

**Decision**: The page saves Vietnamese with the new endpoint and English with `PUT /api/categories/{id}/translations/en`;
an emptied English name removes the translation (`DELETE .../translations/en`), and the category falls back to its own text.

**Rationale**: Specs/026 made the category's columns the default-language text and translations separate rows; the page
follows that shape rather than a combined endpoint. English counts as present only when the English list answers with
`language: "en"` - the field specs/026 added for exactly "has nobody written the English yet".

**Alternatives considered**: one endpoint taking both languages - rejected: it would duplicate the translation rules.

---

## D3 - `IsActive` is left alone

**Decision**: No use of `categories.IsActive`.

**Finding**: Every category created through the API is `false` (the entity's default); all 19 rows in the running database
are `false`; no reader filters on it.

**Rationale**: Giving it meaning needs a data fix and a rule for the products filed under an inactive category; the issue
asked for neither. Recorded so the column is not mistaken for a working switch.

---

## D4 - A duplicate slug is a 409

**Decision**: `ConflictException("A category with the address '<slug>' already exists.")` in place of `new Exception(...)`.

**Rationale**: The shared `GlobalExceptionHandler` maps `Ecommerce.Shared.Exceptions` only; a bare `Exception` became a 500
with no words - the #28 class of defect, found while reading the create handler for this feature.
