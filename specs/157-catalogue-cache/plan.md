# Implementation Plan: The catalogue's public reads, served from memory

**Branch**: `feature/361-catalogue-cache` | **Date**: 2026-10-08 | **Spec**: [spec.md](spec.md) | **Issue**: #361

## Summary

Output caching in Catalog for the four anonymous public GETs, varying by query, negotiated language and currency, 30 s
expiry; an EF Core interceptor evicts everything after any committed write to the catalogue's tables. Measured with the
browse load test.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: ASP.NET Core output caching (in the shared framework), EF Core interceptors
**Storage**: process memory (the output cache's default store)
**Testing**: `CatalogueCacheTests` (TestServer), `CatalogueWritesTests` (PostgreSQL); `loadtest/run.sh browse`
**Constraints**: never stale after a committed write on the same instance; never a signed-in answer; nothing that
decides (pricing, stock, ownership) cached
**Scale/Scope**: one interceptor, one registration, four attributes, a setting, tests, docs

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog caches its own reads and evicts on its own writes; no other service is told or asked. |
| **II. Clean Architecture Layering** | **Pass.** `ICatalogueReadCache` in Application; the interceptor in Infrastructure (it is about EF); the output cache store and policy in WebApi (they are about HTTP). |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Eviction happens after the commit and changes no data; a rolled-back write evicts nothing. The outbox and inbox are untouched. |
| **IV. Identity Comes From the Token** | **Pass.** A request with a token is never cached, so no answer shaped by one caller's identity reaches another. |
| **V. Evidence Over Assumption** | **Pass.** Eviction is tested against PostgreSQL on every write path kind; the gain is measured over several warm load runs before and after. |

**Post-design re-check**: see `tasks.md`.

## Project Structure

```text
specs/157-catalogue-cache/
server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/ICatalogueReadCache.cs
server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/CatalogueWrites.cs   the interceptor
server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/DependencyInjection.cs           registers it on the context
server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Caching/CatalogueCache.cs                 policy, store, setting
server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Program.cs, Controllers/{Products,Categories,Shops}Controller.cs
server/src/Services/Catalog/Ecommerce.Catalog.WebApi/appsettings.json                         Caching:CatalogueSeconds
server/tests/Ecommerce.Catalog.Tests/CatalogueCacheTests.cs, CatalogueWritesTests.cs
docs/features/catalog.md, docs/testing/load-test-results.md, CLAUDE.md, timeline, backlog
```

## Complexity Tracking

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| None | - | - |
