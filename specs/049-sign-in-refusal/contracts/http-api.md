# HTTP Contract: Sign-in refusal

> Written on 2026-09-27, after the feature merged (#130), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Feature**: [spec.md](../spec.md)

One endpoint's refusal gains fields. No endpoint, route or status code was added or removed, and no
message or gRPC contract changed.

## `POST /api/auth/login` - anonymous

Through the gateway: `http://localhost:5000/api/auth/login` (Identity on 5056). Rate limited by the gateway
since specs/062; not part of this feature.

**Request** (unchanged):

```json
{ "email": "lan@demo.test", "password": "Right-Passw0rd" }
```

**Responses**:

| Status | When | Body |
| :--- | :--- | :--- |
| `200` | Right password, account neither locked nor banned | `AuthResponse` (unchanged) |
| `400` | Validation failure (empty email or password) | ProblemDetails with `errors` (unchanged) |
| `401` | No such email, **or** wrong password - on any account, locked or not | ProblemDetails, `detail` "Invalid email or password." (unchanged, #28) |
| `403` | Right password, account locked or banned | ProblemDetails with `detail` **and, since this feature, `code`, `until`, `reason`** |

**403, locked** (Production. An illustration: `until` is the value the pull request's end-to-end run
recorded, and the reason is the one Bruno's `a moderator locks the customer` sends):

```json
{
  "title": "Forbidden",
  "status": 403,
  "detail": "This account is locked until 2026-09-26 06:19 UTC: Bruno checks the lock",
  "instance": "/api/auth/login",
  "traceId": "...",
  "code": "AccountLocked",
  "until": "2026-09-26T06:19:12.35927Z",
  "reason": "Bruno checks the lock"
}
```

**403, banned**: `detail` "This account is banned: Fraud", `code` `AccountBanned`, `reason` `Fraud`, and no
`until`.

`title` is `Forbidden`, as the shared handler writes for every 403; the handler sets no `type`. Neither
changed here.

### Rules

- The extensions are written in **every environment**, like the `ForbiddenException` message; they carry
  nothing `detail` does not already say.
- `until` is ISO 8601 UTC with a trailing `Z` (`ForbiddenProblemTests.A_date_fact_is_ISO_8601_in_UTC`).
- A fact never replaces a key the handler writes itself - `traceId`, `errors`
  (`ForbiddenProblemTests.A_fact_cannot_hide_the_trace_id`).
- Adding fields is not breaking: a client reading only `detail` sees what it saw before.

## Any other `ForbiddenException`

Every service's 403 from `ForbiddenException` may now carry facts. At this merge only the sign-in refusal
does; every other 403 is byte-for-byte as before.
