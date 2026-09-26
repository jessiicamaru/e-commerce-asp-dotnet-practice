# Implementation Plan: No reviews off the shelf

> Written on 2026-09-27, after the feature merged (#177), from the code at that merge, the pull request and
> docs/features/ratings-and-reviews.md.

**Branch**: `085-unlisted-review-writes` | **Date**: 2026-09-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/085-unlisted-review-writes/spec.md`

## Summary

Close the write half of specs/081. `ReviewHandlers` gains `OnSale(product) = product.IsListed && product.IsActive`.
`WriteReviewCommand` throws `NotFoundException("Product not found.")` when the product is missing or not on sale,
before the own-product and eligibility checks; `GetMyReviewQuery` reports `eligible: false` for the same products.
No schema, message or route change.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: MediatR 12.4.1, `Ecommerce.Shared` exceptions

**Storage**: PostgreSQL 16, `ecommerce_catalog_db` (5433); no schema change

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Catalog.Tests/UnlistedProductReadsTests`); Bruno

**Target Platform**: Catalog (5057): `PUT` and `GET /api/products/{id}/reviews/mine`

**Project Type**: A handler change inside one service

**Performance Goals**: None; one product lookup the write already made

**Constraints**: The refusal must not tell "off the shelf" from "does not exist"

**Scale/Scope**: Two handlers in one file

## Constitution Check

*GATE: evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. Re-checked after design.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The listing, the review and the eligibility are all Catalog's own data (eligibility fed by Order's `ParcelDeliveredEvent`, specs/046, unchanged) |
| **II. Clean Architecture Layering** | **Pass.** The rule is in the Application handler; no controller or repository change |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The refusal happens before any write; the review's own write and rating recompute (specs/046, 057) are unchanged |
| **IV. Identity Comes From the Token** | **Pass.** The reviewer is still `ICurrentUser` (`Caller()`); nothing is read from the body but the rating and text |
| **V. Evidence Over Assumption** | **Pass.** The test drives the real take-down command and reads the stored rating from PostgreSQL; two mutations; Bruno against the rebuilt Catalog shows the former 403 now 404 |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/085-unlisted-review-writes/
├── spec.md
├── plan.md              # This file
├── research.md          # Three decisions
├── data-model.md        # No schema change
├── quickstart.md
├── contracts/
│   └── http-api.md      # The two review endpoints
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Catalog/Ecommerce.Catalog.Application/Reviews/ReviewFeatures.cs   # OnSale; WriteReview; GetMyReview
server/tests/Ecommerce.Catalog.Tests/UnlistedProductReadsTests.cs                     # the new test and RatingAsync
bruno/seller/a review of it is a 404.yml                                              # seq 82
```

Docs in the same change: `CLAUDE.md`, `docs/features/ratings-and-reviews.md` (rule 16),
`docs/overview/project-overview.md`, `docs/testing/testing-strategy.md`, `docs/project/timeline.md`,
`docs/project/backlog.md`.

**Structure Decision**: the test joins `UnlistedProductReadsTests`, the file specs/081 opened for what hangs on a
product off the shelf.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

Nothing recorded as left over; the issue is closed.
