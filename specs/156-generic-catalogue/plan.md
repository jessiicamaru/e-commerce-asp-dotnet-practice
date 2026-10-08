# Implementation Plan: A shop for anything, not only cameras

**Branch**: `feature/359-generic-catalogue` | **Date**: 2026-10-08 | **Spec**: [spec.md](spec.md) | **Issue**: #359

## Summary

Reword four interface strings in both languages, replace the aperture in `ProductImage` with a neutral parcel glyph, and
add a wording test that keeps camera words out of the shop's own sentences. Move `cameras.json` into
`server/seed/catalogue/` beside five new verticals, read all of them through one loader (`seed/catalogue.py`) that
validates them and runs as a checker in CI, and point the seeder and the cleaner at it.

## Technical Context

**Language/Version**: TypeScript / React 19 (client); Python 3 (seed); no C# change
**Primary Dependencies**: react-i18next, Vitest; the seed uses the standard library only, as before
**Storage**: none new - products and categories are created through the existing API
**Testing**: a Vitest wording test; `python seed/catalogue.py` (CI); the seeder and cleaner run against the compose stack
**Constraints**: the seed stays API-only and idempotent by SKU; the cameras keep their SKUs and slugs
**Scale/Scope**: ~5 strings × 2 languages, one component, three Python files, five JSON files, docs

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** No service changes; the seed talks to the gateway like any client. |
| **II. Clean Architecture Layering** | **Pass.** Client copy and a shared component; no server layer touched. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The seed goes through the API, so every write takes the existing transactional paths; it stays idempotent by SKU. |
| **IV. Identity Comes From the Token** | **Pass.** The seed signs in as the administrator (with the second factor, from the back office) as before. |
| **V. Evidence Over Assumption** | **Pass.** The data is checked by a validator CI runs; the seeding is judged by running it against the stack twice and the cleaner after it; the wording test is shown failing on the old string. |

**Post-design re-check**: see `tasks.md`.

## Project Structure

```text
specs/156-generic-catalogue/                                  the record
client/packages/core/src/locales/{vi,en}/{common,catalog,seller}.json   the reworded strings
client/packages/core/src/locales/shop-wording.test.ts          no camera words in the shop's own sentences
client/packages/core/src/components/product/product-image/     the parcel tile
server/seed/catalogue.py                                       load + validate every vertical; a checker on its own
server/seed/catalogue/*.json                                   cameras (moved) + five verticals
server/seed/seed-catalogue.py, clean-test-debris.py            read through the loader; seed one vertical by name
.github/workflows/ci.yml                                       the checker in the build job
docs/, CLAUDE.md, server/seed/README.md                        the catalogue is no longer cameras only
```

## Complexity Tracking

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| None | - | - |
