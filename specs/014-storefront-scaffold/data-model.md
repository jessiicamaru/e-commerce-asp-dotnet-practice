# Data Model: A Storefront That Builds and Reaches the Gateway

> Written on 2026-09-27, after the feature merged (#42), from the code at that merge, the pull request
> and docs/architecture/storefront.md (the storefront has no page of its own under docs/features/).

**Feature**: [spec.md](./spec.md)

**No table changed and no migration was added.** The feature touched no service; the only server-side
file in the pull request is the CI workflow.

The client stores nothing - no `localStorage`, no `sessionStorage`, no cookie of its own. What it holds
is in memory, in these types (`client/src/api/http.ts`, `client/src/pages/StatusPage.tsx`):

| Type | Fields | Meaning |
| :--- | :--- | :--- |
| `ProblemDetails` | `title?`, `status?`, `detail?`, `errors?: Record<string, string[]>` | An error body as every service writes it (RFC 7807, shared `GlobalExceptionHandler`) |
| `ApiError` | `status`, `problem: ProblemDetails`, `fieldErrors` (getter) | What `api()` throws for any non-2xx answer; `fieldErrors` is the first message per field |
| `RequestOptions` | `method?`, `body?`, `anonymous?` | `anonymous` skips the bearer token and the refresh-and-retry |
| Status page `Result` | `checking` \| `up` \| `down` + `code` | One service's health as the page shows it |

The token provider and the refresh callback are module-level functions set by `configureAuth`; at this
merge nothing calls it, so the provider returns `null` and every call is effectively anonymous.
