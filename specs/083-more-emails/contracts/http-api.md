# HTTP Contract: Emails for what happens to people

> Written on 2026-09-27, after the feature merged (#171), from the code at that merge, the pull request and
> docs/features/email.md.

**Feature**: [spec.md](../spec.md)

No endpoint was added and no request or response body changed shape. Three endpoints now read a header they did
not read before, and one list grew.

---

## `Accept-Language` recorded on the account

| Endpoint | Access | Before | After |
| :--- | :--- | :--- | :--- |
| `POST /api/auth/register` | anonymous | header used for the confirmation email (specs/063) | also stored as `users.Language` |
| `POST /api/auth/register-seller` | anonymous | the same | the same |
| `POST /api/auth/login` | anonymous | header ignored | stored as `users.Language` |
| `POST /api/auth/refresh` | refresh cookie | header ignored | stored when different |

Only `vi` or `en` (first two letters, any case) is stored; anything else changes nothing. The controller reads the
header (`RequestLanguage()`) and passes it into the command; the body cannot set it. Responses are unchanged.

## `GET /api/email-templates` - `Admin`

Lists every template in every language (specs/077). It now returns 11 templates × 2 languages = 22 entries, in the
console's order: `OrderPaid`, `ParcelShipped`, `OrderCancelled`, `ReturnAccepted`, `ReturnRefused`,
`ReturnRefunded`, `SavedBackInStock`, `PasswordReset`, `EmailConfirmation`, `AccountLocked`, `AccountBanned`. Each
new entry carries its placeholders (the spec's FR-001 table) and no required placeholder. Bruno's
`admin-users/an administrator lists the emails` asserts all 22 and the new placeholders.

The other `/api/email-templates/{template}/{language}/...` endpoints (versions, save, reset, restore, preview,
test) accept the new template names unchanged; preview and test use each template's sample data.

## No new route at the gateway

All of the above were already routed to Identity.
