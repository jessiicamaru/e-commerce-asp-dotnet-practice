# Implementation Plan: Honest view counts

> Written on 2026-09-27, after the feature merged (#178), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Branch**: `086-product-view-limits` | **Date**: 2026-09-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/086-product-view-limits/spec.md`

## Summary

Two guards, each for the case the other cannot stop. **At the gateway**, a fourth rate-limit policy, `views`
(30 a minute per client), on a new route that matches only `POST /api/products/{id}/view`. **In Catalog**, a view
counts once per viewer per shop day: the handler hashes the token's user id, or else the body's visitor id, and the
repository runs one statement - a data-modifying CTE that deletes the product's earlier days from
`product_viewers`, inserts the viewer with `ON CONFLICT DO NOTHING RETURNING 1`, and increments `product_views` only
from that returned row. A call with no viewer keeps the old upsert. The storefront makes and keeps the visitor id.
One additive migration (a new table).

## Technical Context

**Language/Version**: C# 13 / .NET 10.0; TypeScript (React 19) in the storefront

**Primary Dependencies**: YARP and ASP.NET Core rate limiting (`RateLimiterPolicy`, fixed window) at the gateway;
EF Core with Npgsql (`ExecuteSqlInterpolatedAsync`); `System.Security.Cryptography.SHA256`; `crypto.randomUUID`
and `localStorage` in the browser

**Storage**: PostgreSQL 16, `ecommerce_catalog_db` (5433): `product_viewers` added; `product_views` unchanged

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Catalog.Tests/ProductViewTests`, concurrent calls);
`Ecommerce.ApiGateway.Tests` through WebApplicationFactory (no database); Vitest; Bruno

**Target Platform**: ApiGateway (5000), Catalog (5057), the storefront's product page

**Project Type**: Gateway configuration, one service change, one client utility

**Performance Goals**: None stated. A view is still one database round trip

**Constraints**: The endpoint stays anonymous and always 204 (specs/047); no read-then-write; the body may never
override the token (Constitution IV); nothing stored names a person

**Scale/Scope**: At most a day of viewer rows per viewed product

## Constitution Check

*GATE: evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. Re-checked after design.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The viewers and counts are Catalog's; the gateway limit keeps no data beyond an in-memory counter per client |
| **II. Clean Architecture Layering** | **Pass.** `ProductViewer` in Domain; the hashing and the choice of token over body in the Application handler; the one SQL statement in Infrastructure's `ProductViewRepository` behind `IProductViewRepository`; the controller only binds an optional body |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** No message. The claim and the count are one statement: the viewer row's primary key is the guard, so a repeat affects zero rows in `product_viewers` and therefore adds nothing to `product_views` - enforced by a database constraint, as the principle requires |
| **IV. Identity Comes From the Token** | **Pass, and stated in the code.** A signed-in viewer is `ICurrentUser.Id`, whatever the body says; the body's `viewer` is used only when there is no token, and it identifies a browser, not a user. A mutation making the body win over the token was run and turned the tests red |
| **V. Evidence Over Assumption** | **Pass.** Twenty concurrent calls against a real PostgreSQL; the gateway's real pipeline in a test; Bruno against the rebuilt gateway, catalog and storefront reading "most viewed" back; five mutations |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/086-product-view-limits/
├── spec.md
├── plan.md              # This file
├── research.md          # Six decisions
├── data-model.md        # product_viewers and the recording statement
├── quickstart.md
├── contracts/
│   └── http-api.md      # The view endpoint's body and the gateway's 429
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/ApiGateway/Ecommerce.ApiGateway/
├── AuthRateLimits.cs            # Views = "views", (30, 60)
└── appsettings.json             # catalog-product-view-route, RateLimiterPolicy "views"
server/src/Services/Catalog/
├── Ecommerce.Catalog.Domain/Entities/ProductViewer.cs                      # new
├── Ecommerce.Catalog.Application/
│   ├── Common/Interfaces/IProductViewRepository.cs                        # RecordAsync(..., viewer)
│   └── Products/Views/ProductViewFeatures.cs                              # Viewer, ProductViewRequest, ViewerOf
├── Ecommerce.Catalog.Infrastructure/
│   ├── Configurations/ProductViewerConfiguration.cs                      # new
│   ├── Persistence/CatalogDbContext.cs                                   # ProductViewers
│   ├── Persistence/Repositories/ProductViewRepository.cs                 # the CTE
│   └── Migrations/20260926174014_AddProductViewers.cs
└── Ecommerce.Catalog.WebApi/Controllers/ProductsController.cs            # optional body
server/tests/Ecommerce.ApiGateway.Tests/AuthRateLimitTests.cs
server/tests/Ecommerce.Catalog.Tests/ProductViewTests.cs
client/src/utils/shared/visitor.ts (new), client/src/utils/shared/index.ts
client/src/services/product/index.ts (+ index.test.ts)
bruno/admin-insights/a shopper opens the product page.yml, bruno/admin-insights/the most viewed products.yml
```

Docs in the same change: `CLAUDE.md`, `docs/features/admin-insights.md`,
`docs/features/auth/security-best-practices.md`, `docs/architecture/microservices-design.md`,
`docs/project/decisions.md` (row 67), `docs/reference/{api,data-model,gateway}.md`,
`docs/overview/project-overview.md`, `docs/testing/testing-strategy.md`, `docs/project/timeline.md`,
`docs/project/backlog.md`.

**Structure Decision**: the policy joins the three of specs/062 in `AuthRateLimits` rather than a new class (spec
Decisions); the visitor id is a `utils/shared` function, beside the other small browser helpers.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

A visitor who clears their storage, or a script sending a new id each time, counts as a new viewer; the gateway's
limit is what bounds them. Telling bots from people is out of scope.
