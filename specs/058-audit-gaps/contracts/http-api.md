# HTTP Contract: Audit gaps

> Written on 2026-09-27, after the feature merged (#141), from the code at that merge, the pull request and
> [docs/features/audit-and-notifications.md](../../../docs/features/audit-and-notifications.md).

**Feature**: [spec.md](../spec.md)

No endpoint was added, and no request or response shape changed. What changed is what these existing endpoints
leave on the record, and what one of them does to other sessions. All are reached through the gateway on `:5000`.

| Endpoint | Auth | Now also |
| :--- | :--- | :--- |
| `PUT /api/categories/{id}/translations/{language}` | Admin | records `CategoryTranslated` |
| `DELETE /api/categories/{id}/translations/{language}` | Admin | records `CategoryTranslationRemoved` |
| `DELETE /api/products/{id}/translations/{language}` | Seller (own product) or Admin | records `ProductTranslationRemoved` |
| `PUT /api/addresses/{id}/default` | signed in | records `DefaultAddressChanged` (nothing when it already was the default) |
| `POST /api/auth/logout` | the `refreshToken` cookie | records `SignedOut` (nothing when no session was ended) |
| `POST /api/auth/refresh` | the `refreshToken` cookie | see below |

## `POST /api/auth/refresh` with a revoked token

The response is unchanged in every case: **401** ProblemDetails with the same message, so the caller learns
nothing about which case it hit.

| Token | Server-side effect before #141 | After |
| :--- | :--- | :--- |
| Rotated, presented again within 10 s | none | none |
| Rotated, presented again after 10 s | every session revoked, warning logged | the same, plus `SessionReuseDetected` recorded first |
| Revoked without rotation (lock, ban, earlier sweep) | every session revoked, warning logged | Information logged, **nothing revoked** |

## Reading the result

`GET /api/audit?action=<Action>&subjectId=<id>` (Admin, Activity, specs/041) returns the entries; unchanged.
