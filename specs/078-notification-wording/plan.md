# Implementation Plan: An administrator rewords the notifications

**Branch**: `078-notification-wording` | **Spec**: [spec.md](spec.md) | **Issue**: #150 (closes it)

## Design (Activity)

- `NotificationWordingVersion` and the `notification_wording_versions` table (the migration
  `AddNotificationWordingVersions`), shaped like specs/077's email versions.
- `NotificationContract` (Shared) reads the new `placeholders` section of `notification-kinds.json`.
  `PlaceholdersFor(kind)` returns the placeholders whose data keys the kind carries.
- `NotificationWording` is pure code: the kind of a key (with a plural suffix), the placeholders a text uses, and
  links that leave the web or the shop.
- `NoticeSanitizer` (Ganss.Xss) keeps `strong`, `b`, `em`, `i`, `u` and `a[href]` with `http`/`https`.
  - `KeepChildNodes` is on, so the paragraph an editor wraps a line in gives up its words rather than losing them.
  - The text of a script or a style is dropped with it.
- `NotificationWordingHandlers` cover the public current wording, the admin overview, the versions, save, reset and
  restore. Every change is a guarded insert, with its audit entry staged in the same transaction. Activity now
  registers `AddAuditTrail("activity")`, and its own consumer records the entries.
- `NotificationWordingController`, at `api/notifications/wording`: `GET` is anonymous and cached for 60 seconds,
  and everything else is `Admin`. The existing `/api/notifications/*` gateway route carries it.

## Storefront

- `services/notification-wording` and `hooks/notification-wording`.
- `utils/notifications/wording.ts`: `applyWording(overrides)` lays the edits over the **bundled** words for each
  language, bundled first, so a reset (a key no longer edited) goes back to them.
  - i18n re-renders on store changes (`react.bindI18nStore: 'added'`).
- `describeNotification` escapes the values it fills in and returns HTML. `NoticeText` renders it through DOMPurify
  with the server's allow-list. The bell and the notifications page use it.
- `RichTextEditor` gains `variant="inline"`: bold, italic, underline and link only. Its output is one line, with
  paragraphs joined, and placeholders are written `{{name}}`.
- `pages/admin-wording` (`/admin/notifications`, administrators only, lazy-loaded):
  - kinds and keys, each key as the bundled or the edited words in both languages;
  - edit with the inline editor, the placeholders of that kind, and a live sample rendered by
    `describeNotification` with made-up data;
  - save, reset, and versions with restore.
- `utils/notifications/index.test.ts` gains the check that `describeNotification` fills exactly the placeholders
  `notification-kinds.json` declares.

## Research

- **D1 - Activity, not Identity.** Notifications live in Activity; only emails needed Identity (it knows
  addresses).
- **D2 - the server stores edits, the bundle is the default.** A new kind needs no server change to have words, and
  an unreachable Activity leaves every notice readable.
- **D3 - escape the values, sanitise the words.** A product name is whatever a seller typed, and now that notices
  render HTML it must never become markup.
