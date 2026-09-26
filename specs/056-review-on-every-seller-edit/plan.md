# Implementation Plan: Review on every seller edit

> Completed on 2026-09-27, after the feature merged (#139), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Branch**: `056-review-on-every-seller-edit` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md) | **Issue**: #126

## Summary

Call `ProductReview.AfterSellerEditAsync` in the two handlers that skipped it - translating a variant option and
adding a variant - before their one save, and give the option translation the audit entry every other
translation already had. The rule itself does not change. `AfterSellerEditAsync` is now called in eight
handlers.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: MediatR 12.4.1, `Ecommerce.Shared.Audit` (`IAuditTrail`), MassTransit outbox (audit
entries are published through it)

**Storage**: PostgreSQL, `ecommerce_catalog_db` (5433) - `products.ReviewStatus`, `ReviewReason`, `SubmittedAt`;
no schema change

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Catalog.Tests`, `ProductReviewTests`) with MassTransit's
harness to read published audit entries

**Target Platform**: Catalog service (5057)

**Constraints**: the change, its audit entries and the resubmission commit with the one save (Principle III)

**Scale/Scope**: two handlers, one new audit entry

## Design

- `SetOptionTranslationCommandHandler` gains `IAuditTrail`. It records `OptionTranslated`, before and
  after, then calls `AfterSellerEditAsync`, then saves once.
- `AddProductVariantCommandHandler` calls `AfterSellerEditAsync` before its save, after its existing
  audit entry.
- The guard lives inside `AfterSellerEditAsync`: approved products only, sellers' products only, never
  for an administrator. No handler repeats it.

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md); re-checked after design.*

- III (atomic writes): the audit entries, the event and the review change all commit with the one save.
  Pass.
- V (evidence): the two tests fail before the calls exist, and a mutation check removes each call.
  Pass.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog owns products and their review status; nothing crosses a service. The audit entry goes to Activity as a message, as every other does |
| **II. Clean Architecture Layering** | **Pass.** Both calls are in Application-layer handlers; the rule stays in `Application/Common/ProductReview.cs` |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** `OptionTranslated`, `VariantAdded` and `ProductSentForReview` are published through the outbox and staged before the handler's single `SaveChangesAsync`, together with the translation or variant and the status change |
| **IV. Identity Comes From the Token** | **Pass.** Who is editing - seller or administrator - comes from `ICurrentUser`; ownership is still `SellerOwnership`'s 404 |
| **V. Evidence Over Assumption** | **Pass.** Both tests failed before the fix for the right reason (`Approved` where `Pending` was expected); two mutations removing each call were each caught; the doc's claim that new variants were exempt "as decided with the user" was checked against specs/045 rather than trusted, and corrected |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/056-review-on-every-seller-edit/
├── spec.md
├── plan.md            # This file
├── research.md        # D1-D3
├── data-model.md      # No schema change; the status transition and the audit entries
├── quickstart.md
├── contracts/
│   └── http-api.md    # Two endpoints' new side effect
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

- `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Translations/SetProductTranslationCommand.cs`
  (`SetOptionTranslationCommandHandler`)
- `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Variants/AddProductVariant/AddProductVariantCommand.cs`
- `server/tests/Ecommerce.Catalog.Tests/ProductReviewTests.cs`
- `CLAUDE.md` ("eight handlers"), `docs/features/catalog.md`, `docs/features/audit-and-notifications.md`,
  `docs/overview/project-overview.md`, `docs/testing/testing-strategy.md`, `docs/project/backlog.md`,
  `docs/project/timeline.md`

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- The rule is still a call each handler must remember. CLAUDE.md now says a *ninth* edit of what a shopper reads
  must call it too; nothing enforces that automatically.
