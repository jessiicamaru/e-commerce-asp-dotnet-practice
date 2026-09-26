---
description: "Task list for Review on every seller edit"
---

# Tasks: Review on every seller edit

> Completed on 2026-09-27, after the feature merged (#139), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written first - both failed before the fix.

## Format: `[ID] [P?] [Story] Description`

- [X] T001 [US1] [US2] Tests first in `server/tests/Ecommerce.Catalog.Tests/ProductReviewTests.cs`: option translation and a new variant each send an approved seller product back; staff do not; the option translation is audited
- [X] T002 [US1] [US2] `SetOptionTranslationCommandHandler` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Translations/SetProductTranslationCommand.cs`; `AddProductVariantCommandHandler` in `.../Products/Variants/AddProductVariant/AddProductVariantCommand.cs`
- [X] T003 Mutation checks; docs `docs/features/catalog.md`, `docs/project/*`, CLAUDE.md ("eight handlers")
- [X] T004 [P] Correct `docs/features/catalog.md`'s claim that new variants were exempt "as decided with the user", after checking specs/045; `docs/features/audit-and-notifications.md` (`OptionTranslated`), `docs/overview/project-overview.md`, `docs/testing/testing-strategy.md`
- [X] T005 Merged as #139 (2026-09-24), closing #126

## Verification recorded in #139

- Both new tests failed before the fix, for the right reason: `Approved` where `Pending` was expected.
- `Ecommerce.Catalog.Tests` 157/157 against real PostgreSQL.
- Mutations, each restored: remove the call in adding a variant - red; remove the call in the option
  translation - red.

## Notes

T004 and T005 were added on 2026-09-27 from the pull request.
