# Phase 0 Research: An administrator rewords the notifications

> Written on 2026-09-27, after the feature merged (#162), from the code at that merge, the pull request and docs/features/audit-and-notifications.md.

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date of the decisions**: 2026-09-26

D1 to D3 are the three decisions `plan.md` held inline under *Research*, carried here with their reasoning. The rest
are decisions the code and the pull request make without naming them as such. Who took each decision is not
recorded beyond the pull request's author; where the record names no rejected alternative, this file says
"not recorded" rather than inventing one.

---

## D1 - Activity, not Identity

**Decision**: The versions live in **Activity** (`notification_wording_versions` in `ecommerce_activity_db`), and the
endpoints sit under Activity's `/api/notifications`.

**Rationale**: Notifications live in Activity; only emails needed Identity (it knows addresses). Activity already
keeps every notice (specs/042) and serves `/api/notifications/*`, and the gateway already routes that path to it, so
the feature needed no new gateway route, no new cluster and no new synchronous edge. It is also the one fact's one
owner (Principle I): whoever keeps the notices keeps what they say.

**Alternatives considered**:

- **Identity, beside the email templates of specs/077.** Rejected: Identity keeps the emails only because it is the
  one service that knows addresses. Notices have nothing to do with addresses, and putting their words in Identity
  would split the notification feature across two services.
- **Leave the words in the storefront bundle** (the state before). Rejected by #150 itself: rewording a notice, or
  fixing a Vietnamese typo, needed a storefront release.

---

## D2 - The server stores edits; the bundle is the default

**Decision**: `client/src/locales/{en,vi}/notifications.json` stay the words of every kind. The server holds only
**edits**. `GET /api/notifications/wording` returns only keys whose current version is an edit, and the storefront
lays them over the bundle - bundle first, edits on top - with `i18n.addResourceBundle(..., deep, overwrite)`
(`applyWording`).

**Rationale**: A new kind needs no server change to have words, and an unreachable Activity leaves every notice
readable. Laying the bundle first on every refresh is what makes a **reset** work in an open page: a key reset since
the last load is simply absent from the answer, so the bundle's words come back rather than the stale edit staying in
i18n (`wording.test.ts`, "goes back to the bundled words once an edit is gone").

**Alternatives considered**:

- **Move every kind's words to the server** (seed the table from the bundle). Rejected: a new kind would have no words
  until somebody seeded them, and Activity down would mean blank notices - the two outcomes the decision exists to
  avoid.
- **Apply only the edits, without re-laying the bundle.** Rejected in the code's own comment: a key reset since the
  last load would keep its stale edit.

---

## D3 - Escape the values, sanitise the words

**Decision**: `describeNotification` escapes each value it fills in (`&`, `<`, `>`, `"`, `'` - `escapeHtml`) **before**
passing it to i18next, and returns HTML. Every place that shows a notice renders that HTML through `NoticeText`, which
runs DOMPurify with the server's allow-list.

**Rationale**: A product name is whatever a seller typed, and now that notices render HTML it must never become markup.
The storefront's i18next config has `escapeValue: false` - React escapes what it renders, and i18next escaping again
"would show `&#39;` to the customer" (the config's comment). Once the output goes through `dangerouslySetInnerHTML`,
React no longer escapes it, so the values must be escaped by hand, and only the values: the administrator's words are
meant to carry markup.

**Alternatives considered**:

- **Turn on i18next's `escapeValue`.** Rejected: it is global, and every other translated string rendered as React
  text would show escaped entities to the customer.
- **Rely on DOMPurify alone.** Not named as an alternative in the record. The pull request's requirement rules it
  out: a product named `<img onerror>` must *show as text*, and a sanitiser removes such markup rather than showing it;
  a value that happened to be allowed markup (`<strong>`) would also render as emphasis nobody wrote.
- **Keep notices as plain text.** Rejected by the issue: an administrator asked for emphasis and links.

The mutation check in #162 - removing the escaping - turned the storefront's test red.

---

## D4 - The placeholder map lives in `notification-kinds.json`

**Decision**: A new `placeholders` section in `Ecommerce.Shared/Notifications/notification-kinds.json` names, for each
`{{placeholder}}`, the data keys it is made from. `NotificationContract.PlaceholdersFor(kind)` returns the placeholders
whose keys the kind carries - **required or optional**. The server refuses words using any other; a client test holds
`describeNotification`'s fillings (`FILLED_PLACEHOLDERS`) and every bundled sentence to the same section.

**Rationale**: It sits beside the keys each kind carries (specs/048), so the server checking and the storefront filling
cannot disagree without a test going red. And since the bundled words are also held to it, a reset always lands on
words the server would accept.

Optional keys count: `ParcelShipped` names its shop when it has one, and the storefront words the shop's own parcel
otherwise, so `{{shop}}` is a legitimate placeholder for it. A mutation that ignored optional keys **survived at
first**; the `ParcelShipped` assertion in `The_overview_lists_every_kind_with_what_its_words_may_use` now pins it.

`count` is made from `rating`: i18next's plural forms read `count`, and the storefront passes the rating as `count`, so
`NewReview_one` / `_other` may use `{{count}}`.

**Alternatives considered**:

- **A placeholder list on each side** (in the server's code and in the storefront's). Rejected: two lists disagree
  silently - the defect specs/048 was written to end, where five kinds showed `{{placeholders}}` to real people.
- **Only required keys make a placeholder usable.** Rejected: `ParcelShipped`'s `{{shop}}` would be refused though the
  storefront fills it.

---

## D5 - Versions, append-only, decided by the database

**Decision**: Every save, reset and restore **adds** a row. The editor sends the version it opened (`expectedVersion`);
the handler refuses anything else with 409, and the insert is `INSERT ... ON CONFLICT ("Key", "Language", "Version")
DO NOTHING`, so of two saves on top of the same version exactly one inserts. The audit entry is staged and saved only
when the insert happened, inside the same transaction, inside `CreateExecutionStrategy().ExecuteAsync`.

**Rationale**: Shaped like specs/077's email versions, so the two editors behave alike. The stale check alone is a
read-then-write that two requests can both pass; the unique index is what decides between them (Principle III). A reset
is a version with `IsDefault = true` and no text, so undoing a reset is an ordinary restore.

**Alternatives considered**:

- **One row per key, updated in place.** Not named in the record; ruled out by US3, which requires every change to be a
  version and any version restorable.
- **Last writer wins, no `expectedVersion`.** Not named in the record; ruled out by the spec's 409 on a stale version,
  which is how specs/077 already worked.

---

## D6 - Emphasis and links only, sanitised twice

**Decision**: The allow-list is `strong`, `b`, `em`, `i`, `u` and `a[href]`. On save, `NoticeSanitizer` (Ganss.Xss
`HtmlSanitizer` 9.2.1039) keeps those tags and only `href`, with schemes `http`/`https`, no CSS, no classes, and
`KeepChildNodes = true`; the text of a `script`, `style`, `iframe`, `object`, `template` or `noscript` is dropped with
it. `NotificationWording.BadLinks` then refuses any address that is not `http(s)://` or a single-slash path of the shop.
In the page, `NoticeText` runs DOMPurify with the same tags, only `href`, and `ALLOWED_URI_REGEXP = /^(?:https?:\/\/|\/(?!\/))/i`.
Text is at most 500 characters.

**Rationale**: A notice is one line in a bell, never a layout. `KeepChildNodes` means the paragraph an editor wraps a
line in gives up its words rather than losing them; a script's words are not words anybody wrote. `//elsewhere` is
refused because a browser reads it as another site. Sanitising again in the page means what runs there does not depend
on how a row was written - "whatever the server did - this is what runs in the reader's page" (`NoticeText`).

**Alternatives considered**:

- **Sanitise in one place only.** Not named as an alternative in the record; the pull request states both passes as
  the design ("applied on save (Ganss.Xss) and again in the page (DOMPurify)"), and the mutation that removed the
  server's sanitising was caught by `NotificationWordingTests`.
- **The email editor's wider allow-list (specs/077).** Not named as an alternative; FR-004 rules it out - "no blocks,
  no images, no styles" in one line of a bell.

---

## D7 - A link shows as its words inside something already clickable

**Decision**: `NoticeText` takes `links` (default `false`). The bell and `/notifications`, where each notice is a link
or a button, drop the anchor and keep its words; the console's sample and version list pass `links`.

**Rationale**: A link inside a link or a button is two controls in one, and the notice's own link already goes where it
is about.

**Alternatives considered**: Keeping the anchors in the bell - rejected for the nested-control reason above. No other
alternative recorded.

---

## D8 - Audited through Activity's own outbox

**Decision**: Activity now registers `AddAuditTrail("activity")`. A wording change publishes `AuditEntryRecorded`
(category `System`, subject `NotificationWording`, `{key}/{language}`, actions `NotificationWordingSaved`,
`NotificationWordingReset`, `NotificationWordingRestored`, with before and after) through Activity's transactional
outbox, and Activity's own `RecordAuditEntryConsumer` keeps it.

**Rationale**: "The way every other service's entries arrive" (`Program.cs`): one path into `audit_entries`, with the
diff computed once by the consumer and the insert idempotent on the entry id. Staged in the version's transaction, the
entry commits with its change or not at all (Principle III).

**Alternatives considered**: Writing the `audit_entries` row directly from the handler - not recorded. Leaving wording
changes unaudited - ruled out by US3.

---

## D9 - How the storefront keeps the words current

**Decision**: `useNotificationWording`, called once in `MainLayout`, fetches the public wording at start and every
5 minutes (`WORDING_REFRESH_MS`), `retry: false`; i18n is configured with `react.bindI18nStore: 'added'` so a bundle
added at run time re-renders what shows it. The server answers with `Cache-Control: public, max-age=60`.

**Rationale**: An edit reaches a new page at once and an open page within the refresh; a failed fetch leaves the bundle
in place (D2). Without `bindI18nStore`, words laid over the bundle after the first render would not appear until
something else re-rendered.

**Alternatives considered**: Not recorded. A push (the bell's own polling, or a socket) was not discussed in the record.

---

## D10 - The email editor, narrowed

**Decision**: The TipTap `RichTextEditor` from specs/077 gains `variant="inline"` (bold, italic, underline and link;
no headings, lists, quotes, rules, strike or hard breaks) and a `token` prop. The notice editor writes placeholders as
`{{name}}`, and `oneLine` joins the paragraphs the editor keeps its content in into one line before `onChange`. The
page is lazy-loaded, like `/admin/emails`, so no shopper downloads the editor.

**Rationale**: One editor to maintain; placeholders spelled the way i18next reads them (`{{name}}`), where the emails
use `{name}`.

**Alternatives considered**: A plain text box - not recorded.

---

## D11 - How this is tested

**Decision**: `NotificationWordingTests` (7) in `Ecommerce.Activity.Tests` against a real PostgreSQL on 5440, with
MassTransit's test harness registered in `ActivityTestFixture` so the audit entries published through the outbox can be
read; Vitest for the storefront (placeholders against the json, escaping, `NoticeText`, `applyWording`, the console);
Bruno `admin-users/` 24-30 through the gateway; and mutation checks.

**Rationale**: The guarantee that two saves make one version belongs to the database's unique index and `ON CONFLICT`,
so an in-memory provider would pass against code without it (Principle V). The mutation of removing `ON CONFLICT` was
caught.

**Alternatives considered**: An in-memory EF provider - rejected for the reason above, as elsewhere in this repository.

---

## Open risks

| Risk | Impact | Mitigation |
| :-- | :-- | :-- |
| The public read carries `Cache-Control: public, max-age=60` | A browser or a proxy may serve words up to a minute old | Accepted; the refresh is 5 minutes anyway. Not measured |
| The two allow-lists (Ganss.Xss, DOMPurify) are configured separately | They could drift | Both are tested (`NotificationWordingTests`, `notice-text/index.test.tsx`); nothing ties the two lists together in one file |
| The languages are a list in code (`NotificationWording.Languages`) | A third language needs a code change on both sides | The bundle has only two locale files; out of scope |
