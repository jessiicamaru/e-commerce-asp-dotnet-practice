# Feature Specification: An administrator rewords the notifications

**Feature Branch**: `078-notification-wording` | **Created**: 2026-09-26 | **Issue**: #150 (the notification half;
closes it, after the emails in specs/077)

## Why

The words of the 30 notification kinds live in the storefront's locale files, which are built into the bundle.
Rewording one, or fixing a Vietnamese typo in one, needs a storefront release.

## User Scenarios

### US1 - Reword a notice (P1)

An administrator opens **Notifications** in the admin console, picks a kind and a language, and edits its sentence
with a small rich-text editor: bold, italic, underline and links only.
- Placeholders such as `{{product}}` are inserted from the list of what that kind carries.
- After saving, the bell shows the new words on the next load, in the reader's language, with no release.

### US2 - Safe by construction (P1)

- A placeholder the kind does not carry is refused, and the refusal names it.
- The stored text is sanitised on save and again in the storefront before it is shown. The values filled in are
  escaped, so a product called `<img onerror>` is shown as those characters.
- A notice with a hole in it still falls back to the generic sentence (specs/048).

### US3 - Default and undo (P2)

- The bundled words stay the default: an unedited kind reads exactly as today.
- **Reset to default** exists, every change is a version, and any version can be restored.
- Every save, reset and restore is audited with its before and after.

### US4 - Administrators only (P1)

A moderator gets 403 on every write and on the administrator's view. Reading the current wording is public, since
the storefront needs it before anybody signs in.

## Requirements

- **FR-001** `notification_wording_versions (Id, Key, Language, Version, IsDefault, Text, CreatedAt, CreatedBy)` in
  **Activity**, the service that keeps notifications. It is unique on `(Key, Language, Version)`, append-only, and
  versioned the way the emails are (specs/077).
- **FR-002** A key is a declared kind, or a kind plus an i18next plural suffix: `NewReview_one`, `NewReview_other`.
- **FR-003** The placeholders a kind may use are declared in `notification-kinds.json`, in a new `placeholders`
  section. Each placeholder names the data keys it needs, for example `order` needs `orderId`, and `total` needs
  `total` and `currency`.
  - A kind may use a placeholder when it carries all of those keys.
  - The storefront's `describeNotification` fills exactly these names, and its test holds the two together.
- **FR-004** The allow-list is `strong`, `b`, `em`, `i`, `u` and `a[href]`.
  - Links may be `http`, `https`, or relative to the storefront (`/orders`).
  - Nothing else is kept: no blocks, no images, no styles.
  - The text is at most 500 characters.
- **FR-005** The endpoints:

  | Method | Path | Who | What |
  | :-- | :-- | :-- | :-- |
  | `GET` | `/api/notifications/wording` | Anyone | `{ lang: { key: text } }`, the current edits only. Cached for a minute. |
  | `GET` | `/api/notifications/wording/all` | Admin | The kinds and their placeholders, and every key's current version. |
  | `GET` | `/api/notifications/wording/{key}/{lang}/versions` | Admin | The history. |
  | `PUT` | `/api/notifications/wording/{key}/{lang}` | Admin | Save; a stale version is 409. |
  | `POST` | `/api/notifications/wording/{key}/{lang}/reset` | Admin | Back to the bundled words. |
  | `POST` | `/api/notifications/wording/{key}/{lang}/versions/{v}/restore` | Admin | Restore an earlier version. |

- **FR-006** Storefront:
  - The app loads the wording at start and every few minutes, and lays it over the bundled words in i18n. The
    bundled words stay underneath as the fallback.
  - Notices render as sanitised rich text (DOMPurify, the same allow-list), with values escaped.
  - `/admin/notifications` (administrators only) edits them, with the inline editor, the placeholders, a live
    sample, reset and versions.
  - Vitest tests.

## Decisions

- **Activity stores it.** It already owns notifications and their API, and the gateway already routes
  `/api/notifications`.
- **The bundle stays the default, and the server stores only edits.** A new kind ships with words in the
  storefront as today, and a server that is down means the bundled words, never blank notices.
- **The placeholder map lives in `notification-kinds.json`**, beside the keys each kind carries, so the server
  checking and the storefront filling cannot disagree without a test going red.

## Out of scope

Adding kinds from the console, per-person notification preferences, and sending notices by email.
