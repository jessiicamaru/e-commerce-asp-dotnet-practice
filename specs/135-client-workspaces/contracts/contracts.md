# Contracts: The client becomes a workspace of apps and packages

No HTTP, message or gRPC change.

The internal contract this introduces is the import rule:

| From | May import |
| :-- | :-- |
| `apps/*` | `@/` (its own `src`), `@ecommerce/core/*`, `@ecommerce/ui/*`, `cn` |
| `packages/core` | `@ecommerce/core/*` (itself), `@ecommerce/ui/*`, `cn` |
| `packages/ui` | `cn` and relative imports only |

A test (`packages/core/src/test/layering.test.ts`) fails on any import that breaks it.
