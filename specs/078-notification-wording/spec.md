# Feature Specification: An administrator rewords the notifications

> Completed on 2026-09-27, after the feature merged (#162), from the code at that merge, the pull request and docs/features/audit-and-notifications.md.

**Feature Branch**: `078-notification-wording` | **Created**: 2026-09-26 | **Issue**: #150 (the notification half;
closes it, after the emails in specs/077)

**Status**: Merged (#162, 2026-09-26)

**Input**: Issue #150, second half: "an administrator edits what the shop says to people" - the emails were
[specs/077](../077-email-templates/); this is the in-app notices that [specs/042](../042-in-app-notifications/) shows
in the bell and whose data keys [specs/048](../048-notification-wording/) declared.

## Why

The words of the 30 notification kinds live in the storefront's locale files, which are built into the bundle.
Rewording one, or fixing a Vietnamese typo in one, needs a storefront release.

A notice stores a kind and data, never a sentence (specs/042), so the words are chosen when somebody reads it. That
is what makes rewording possible at all: an edit changes every notice of that kind, old and new, the next time it is
read. It is also what makes it dangerous - the words become HTML shown in every reader's page, and the values filled
into them (a product name, a rejection reason) are whatever another person typed.

## User Scenarios & Testing *(mandatory)*

### US1 - Reword a notice (P1)

An administrator opens **Notifications** in the admin console, picks a kind and a language, and edits its sentence
with a small rich-text editor: bold, italic, underline and links only.
- Placeholders such as `{{product}}` are inserted from the list of what that kind carries.
- After saving, the bell shows the new words on the next load, in the reader's language, with no release.

**Why this priority**: It is the feature. Without it a typo in a Vietnamese notice waits for a storefront release,
which is what #150 asked to end.

**Independent Test**: Save new English words for `NewSale` through `PUT /api/notifications/wording/NewSale/en`, then
read `GET /api/notifications/wording` anonymously: `en.NewSale` is the new words and `vi` has no `NewSale`. Open the
storefront in English and the bell shows the new sentence for a `NewSale` notice.

**Acceptance Scenarios**:

1. **Given** no edit for `OrderPaid`, **When** an administrator saves English words with `{{order}}` and `{{total}}`
   on top of version 0, **Then** version 1 is stored and the public wording carries it under `en` only.
2. **Given** an edit is saved, **When** a reader opens a page, **Then** the storefront lays the edit over its
   bundled words and the bell shows it, in the reader's language.
3. **Given** `NewReview` has plural forms, **When** an administrator edits `NewReview_one`, **Then** that form alone
   changes; `NewReview_other` keeps its words.

---

### US2 - Safe by construction (P1)

- A placeholder the kind does not carry is refused, and the refusal names it.
- The stored text is sanitised on save and again in the storefront before it is shown. The values filled in are
  escaped, so a product called `<img onerror>` is shown as those characters.
- A notice with a hole in it still falls back to the generic sentence (specs/048).

**Why this priority**: The words are shown as HTML in every reader's page, signed in or not. A rewording feature
that lets a script, a link to another site, or a seller's product name become markup is worse than no feature.
Equal first with US1 because US1 cannot ship without it.

**Independent Test**: Save `Sold for {{total}}` for `NewSale` (which carries no total): 400 naming `{{total}}`.
Save words with a `<script>`: the stored text has no script. Render a notice whose product is `<img onerror=x>`:
the reader sees those characters.

**Acceptance Scenarios**:

1. **Given** `ProductApproved` carries only `product`, **When** words using `{{total}}` are saved, **Then** 400 with
   an error on `Text` that starts `{{total}} is not something a ProductApproved notice can fill in` and lists
   `{{product}}` as what it can use.
2. **Given** words containing `<p onclick>`, `<script>`, `<img onerror>`, `<a href="javascript:...">` and `<h1>`,
   **When** they are saved, **Then** what is stored is the words with only the allowed emphasis and a link with no
   address - the script's text gone with it.
3. **Given** a link to `//evil.test/x`, **When** it is saved, **Then** 400 naming the address.
4. **Given** a notice whose data has a product called `<img onerror=x>`, **When** it is shown, **Then** it reads as
   those characters, not as an image.
5. **Given** edited words use a placeholder whose value a particular notice lacks, **When** that notice is shown,
   **Then** it reads the generic sentence rather than a sentence with a hole.

---

### US3 - Default and undo (P2)

- The bundled words stay the default: an unedited kind reads exactly as today.
- **Reset to default** exists, every change is a version, and any version can be restored.
- Every save, reset and restore is audited with its before and after.

**Why this priority**: Rewording without a way back makes every edit a risk; but the shop can reword safely (US1,
US2) before undo exists, so it is second.

**Independent Test**: Save words for `ParcelShipped` (v1), reset (v2), restore v1 (v3): the versions read 3, 2, 1;
after the reset the public wording has no `ParcelShipped`, after the restore it has the v1 words again; three audit
entries were published.

**Acceptance Scenarios**:

1. **Given** no row exists in `notification_wording_versions`, **When** any notice is read, **Then** it reads
   exactly as the bundle says.
2. **Given** an edited key, **When** it is reset, **Then** a version with `IsDefault = true` and no text is added and
   the public wording drops the key, so the storefront goes back to its bundled words.
3. **Given** a key already at its default, **When** it is reset, **Then** 409 "These words are already the
   storefront's own."
4. **Given** an earlier version, **When** it is restored, **Then** its words are checked again against today's rules
   and added as a new version.
5. **Given** two administrators opened the same key at version 1, **When** both save, **Then** the second is 409 and
   nothing of theirs is stored.

---

### US4 - Administrators only (P1)

A moderator gets 403 on every write and on the administrator's view. Reading the current wording is public, since
the storefront needs it before anybody signs in.

**Why this priority**: The words reach every customer; a moderator's powers are over accounts and listings, and the
emails' editor (specs/077) was already administrator-only.

**Independent Test**: As a moderator, `PUT /api/notifications/wording/NewSale/en` is 403. Anonymously,
`GET /api/notifications/wording` is 200.

**Acceptance Scenarios**:

1. **Given** a moderator's token, **When** they save, reset, restore, list versions or open the overview, **Then** 403.
2. **Given** no token, **When** the public wording is read, **Then** 200 with `vi` and `en`.
3. **Given** no token, **When** any administrator endpoint is called, **Then** 401.

---

### Edge Cases

- **An unknown kind, plural suffix or language.** `SomethingNew`, `NewReview_lots` and `OrderPaid` in `fr` are 404
  "No such notification wording." - the same as a missing thing anywhere else.
- **A kind added to the code later.** It has bundled words and no row; it reads from the bundle with no server change.
- **Activity is down.** The storefront's query fails (`retry: false`) and the bundled words stay in i18n - never a
  blank notice.
- **An edit reset since the reader's last load.** The storefront lays the bundle first and the edits over it, so the
  reset key goes back to the bundle rather than keeping the stale edit.
- **An open page.** It asks again every 5 minutes; the public answer carries `Cache-Control: public, max-age=60`, so a
  browser may reuse the previous answer for up to a minute. How long an edit takes to reach an open bell was not
  measured - not recorded.
- **An editor wraps the line in paragraphs.** The inline editor joins them into one line before sending; the server's
  sanitiser drops a paragraph tag and keeps its words.
- **A link inside the bell or the list.** The whole notice is already a link or a button there, so a link in the words
  shows as its words, without the anchor. The console's live sample and version list show links.
- **Two saves of the same key at once.** Both ask for version N + 1; the unique `(Key, Language, Version)` lets one
  insert and the other gets 409, with no audit entry.
- **A version restored from before a rule tightened.** It is sanitised and checked again, so an old version cannot
  bring back what today's rules refuse.
- **An optional data key.** `ParcelShipped` carries `shop` only sometimes; `{{shop}}` is still allowed, and a notice
  without it falls back to the storefront's words for the shop's own parcel (`shop` is filled with "the shop").
- **More than 500 characters, or nothing left after sanitising.** 400 on `Text`.

## Requirements *(mandatory)*

### Functional Requirements

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

  > Corrected on 2026-09-27: `.../all` returns the current version of every key **that has a saved version** (an edit
  > or a reset), not of every key - `NotificationWordingStore.CurrentAllAsync` reads the table, and a key never
  > edited has no row. The console shows the bundled words for the rest. "Cached for a minute" is a
  > `[ResponseCache(Duration = 60, Location = Any)]` header (`Cache-Control: public, max-age=60`); Activity keeps no
  > server-side cache.

- **FR-006** Storefront:
  - The app loads the wording at start and every few minutes, and lays it over the bundled words in i18n. The
    bundled words stay underneath as the fallback.
  - Notices render as sanitised rich text (DOMPurify, the same allow-list), with values escaped.
  - `/admin/notifications` (administrators only) edits them, with the inline editor, the placeholders, a live
    sample, reset and versions.
  - Vitest tests.
- **FR-007** A placeholder the kind cannot fill MUST be refused with a 400 whose message names the placeholder and
  lists what the kind can use. A link that is not `http(s)://` or a single-slash path MUST be refused with a 400
  naming the address.
- **FR-008** Every save, reset and restore MUST carry the version the editor opened (`expectedVersion`); anything
  else is 409 and stores nothing.
- **FR-009** Reset and restore MUST add a version, never delete or rewrite one. A reset of a key already at its
  default is 409. A restored version MUST pass today's sanitising and checks.
- **FR-010** Every stored version MUST be audited in the same transaction as its row, as `NotificationWordingSaved`,
  `NotificationWordingReset` or `NotificationWordingRestored`, with the before and the after.
- **FR-011** The public read MUST need no token; every other endpoint MUST be `Admin` only - not `Moderator`.
- **FR-012** `describeNotification` MUST escape each value (`&`, `<`, `>`, `"`, `'`) before it is placed in the words
  and return HTML; every place that shows it MUST go through `NoticeText`, which sanitises with the same allow-list.
- **FR-013** An unknown key or language MUST be 404.
- **FR-014** Activity being unreachable MUST leave the storefront showing its bundled words.

### Key Entities

- **Notification wording version**: one saved state of one key's words in one language - an administrator's edit, a
  reset to the bundle (`IsDefault`, no text), or an earlier version restored. Numbered from 1 per key and language;
  the highest number is the current words. Append-only.
- **Placeholder declaration**: in `notification-kinds.json`, a `{{name}}` and the data keys it is made from. A kind
  may use it when it carries every one of those keys, required or optional.
- **Bundled words**: `client/src/locales/{en,vi}/notifications.json`, `kind.*`. The default for every key and the
  fallback under any edit.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An administrator rewords a notice and readers see it with no storefront release. Bruno `admin-users/` 28-29
  reword `NewSale` and read the new words back through the rebuilt storefront container; the live look at the bell is
  ticked in T005, with no further detail recorded.
- **SC-002**: With no rows in `notification_wording_versions`, every notice reads exactly as before the feature (the
  migration only adds a table).
- **SC-003**: A placeholder its kind cannot fill is refused 100% of the time, by name - `NotificationWordingTests`
  and Bruno `admin-users/` 27.
- **SC-004**: No markup from a data value reaches a reader's page: the storefront's escaping test fails when the
  escaping is removed (mutation check in #162).
- **SC-005**: Of two saves on top of the same version, exactly one is stored - `TryAddAsync` returns true once for
  one version number.
- **SC-006**: Every stored version has exactly one audit entry, and a refused save has none.
- **SC-007**: A moderator is refused every write (403) - Bruno `admin-users/` 25.

## Assumptions

- Only the two languages the shop speaks, `vi` and `en`, are editable; they are listed in code
  (`NotificationWording.Languages`), like the bundle's two locale files.
- The words of a notice are one line in a bell, so emphasis and links are all it needs.
- The bundle keeps shipping words for every kind; the server never holds a kind's only words.
- `/api/notifications/{**catch-all}` already routes to Activity at the gateway, so no gateway change is needed.
- Administrators are trusted to write the words but not to write markup that runs: sanitising does not depend on who
  saved.

## Decisions

- **Activity stores it.** It already owns notifications and their API, and the gateway already routes
  `/api/notifications`.
- **The bundle stays the default, and the server stores only edits.** A new kind ships with words in the
  storefront as today, and a server that is down means the bundled words, never blank notices.
- **The placeholder map lives in `notification-kinds.json`**, beside the keys each kind carries, so the server
  checking and the storefront filling cannot disagree without a test going red.

The reasoning and rejected alternatives are in [research.md](./research.md).

## Out of scope

Adding kinds from the console, per-person notification preferences, and sending notices by email.

Also outside it, by construction: languages beyond `vi` and `en`, and rewording any storefront text other than the
`kind.*` sentences of the `notifications` namespace.
