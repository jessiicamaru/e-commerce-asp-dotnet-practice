# Implementation Plan: An administrator rewords the notifications

> Completed on 2026-09-27, after the feature merged (#162), from the code at that merge, the pull request and docs/features/audit-and-notifications.md.

**Branch**: `078-notification-wording` | **Spec**: [spec.md](spec.md) | **Issue**: #150 (closes it)

**Merged**: #162, 2026-09-26 (`5a136f0`)

## Summary

The words of the 30 notification kinds move from "only in the bundle" to "in the bundle, with an administrator's
edits laid over them". Activity keeps the edits as append-only versions in `notification_wording_versions`, checks
every one against the placeholders its kind can fill (declared in the shared `notification-kinds.json`) and against an
allow-list of emphasis and links, audits each through its own outbox, and serves the current edits publicly. The
storefront fetches them at start and every 5 minutes, lays them over its bundled words, and renders every notice as
sanitised HTML with the values escaped; administrators edit them at `/admin/notifications`. The second half of #150,
after the emails of specs/077, and shaped like them.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0 (Activity, Ecommerce.Shared); TypeScript, React 19 (storefront)

**Primary Dependencies**: MediatR 12.4.1, FluentValidation 12.1.1, Npgsql EF Core 10.0.3, MassTransit 8.3.6 (EF
outbox), **HtmlSanitizer (Ganss.Xss) 9.2.1039** - new to Activity; storefront: TanStack Query, react-i18next, TipTap
(the editor from specs/077), **dompurify ^3.4.16** - new

**Storage**: PostgreSQL 16, `ecommerce_activity_db` on host port 5440 - one new table
(`20260926112005_AddNotificationWordingVersions`)

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Activity.Tests`, 7 new in `NotificationWordingTests`, 35 in
all) with MassTransit's test harness for the published audit entries; Vitest + Testing Library in `client/`; Bruno
`admin-users/` 24-30 through the gateway; mutation checks (listed in [tasks.md](./tasks.md))

**Target Platform**: Activity on 5063 (container 8080), behind the gateway on 5000; the storefront through Vite's proxy
or the nginx image on 8088

**Project Type**: Existing Clean Architecture service (Activity) plus the React storefront; one shared file
(`notification-kinds.json`) and one shared class (`NotificationContract`)

**Performance Goals**: None stated. The public read is one query over a small table, answered with
`Cache-Control: public, max-age=60`; no latency was measured - not recorded

**Constraints**: An unedited kind reads exactly as before; Activity down must mean the bundled words, never blank
notices; nothing an administrator or a seller types may run in a reader's page; two saves of one version store one;
administrators only (not moderators) for every write

**Scale/Scope**: 30 kinds, plus plural forms, in 2 languages. Six endpoints, one table, one page

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. Written after the merge: the plan
as merged held no Constitution Check, so this is an assessment of the design that shipped, not a gate it passed at the
time.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** One owner for the fact: Activity keeps the notices and now what they say (research D1), in its own database. No new synchronous call and no other service's data read; `CreatedBy` is a user id from the token, not a join to Identity. The only shared change is `notification-kinds.json` in `Ecommerce.Shared`, the cross-cutting declaration specs/048 already put there. The storefront's bundle is a default, not a second owner: the server holds only edits, and nothing decides anything from the bundle |
| **II. Clean Architecture Layering** | **Pass.** `NotificationWordingVersion` in Domain; `INotificationWordingStore` and `INoticeSanitizer` declared in Application `Common/Interfaces`, the queries, commands, validator and handlers in `Application/Notifications`; the store (EF + raw `ON CONFLICT`) and the Ganss.Xss sanitiser in Infrastructure and registered in its `DependencyInjection.cs`; the controller only sends through MediatR. One deviation from the folder convention: the six requests and their handler share one file, `NotificationWordingFeatures.cs`, rather than a folder per use case - a layout choice, not a dependency leak |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** A version row and its `AuditEntryRecorded` commit together: `TryAddAsync` inserts, then stages the audit publish and calls `SaveChangesAsync` once, inside one explicit transaction inside the execution strategy. A repeat is decided by the database: the unique `(Key, Language, Version)` with `ON CONFLICT DO NOTHING` makes a second save of the same version affect zero rows and stage nothing (409). The existing `RecordAuditEntryConsumer` keeps the entry idempotently on its id |
| **IV. Identity Comes From the Token** | **Pass.** Activity already validates with `AddJwtAuthentication`. The administrator is `ICurrentUser.Id`; no command carries a user id. Writes and the overview are `[Authorize(Roles = "Admin")]`; the one anonymous endpoint is declared with `[AllowAnonymous]`. The storefront's `RequireRole` only hides `/admin/notifications` |
| **V. Evidence Over Assumption** | **Pass.** The one-version-per-number guarantee is the database's, and `NotificationWordingTests` runs against a real PostgreSQL; the audit publish is read from the harness, not assumed. Mutation checks showed the tests bite (accepting any key, placeholders, links, sanitising, `ON CONFLICT`, the stale check, defaults shown as edits, optional keys - which survived until pinned - and the client's escaping). What is not verified is said so: no test makes the storefront's wording fetch fail (quickstart scenario 9), and how soon an edit reaches an open bell was not measured |

**Post-design re-check**: no violations. The Complexity Tracking table below is empty because nothing needed
justifying. The one-file layout of the Application requests is noted under Principle II as a deviation from convention,
not from a principle.

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

> Clarified on 2026-09-27: "cached for 60 seconds" is the response header (`[ResponseCache(Duration = 60, Location =
> Any)]`, i.e. `Cache-Control: public, max-age=60`) - Activity registers no response-caching middleware and keeps no
> server-side cache. Relative links (`/orders`) are kept as well as `http`/`https` ones: Ganss.Xss keeps a relative
> `href`, and `NotificationWording.BadLinks` refuses only what is neither `http(s)://` nor a single-slash path.

Details: the table in [data-model.md](./data-model.md), the endpoints in [contracts/http-api.md](./contracts/http-api.md).

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

Added detail, from the code at the merge:

- `useNotificationWording` is called once, in `MainLayout`, so every page lays the edits over the bundle, signed in or
  not; it refreshes every 5 minutes (`WORDING_REFRESH_MS`) with `retry: false`.
- `describeNotification` escapes each value with `escapeHtml` (`&`, `<`, `>`, `"`, `'` - "and nothing else, so é stays
  é") before i18next fills it. The storefront's i18next runs with `escapeValue: false`, because React escapes what it
  renders; once the result is HTML shown through `dangerouslySetInnerHTML`, that escaping is this function's job
  (research D3). It still returns the generic sentence when a placeholder in the words has no value (specs/048).
- `NoticeText` takes `links` (default off): in the bell and `/notifications`, where the whole notice is a link or a
  button, a link in the words shows as its words (research D7). DOMPurify's `ALLOWED_URI_REGEXP` keeps only
  `http(s)://` and single-slash addresses.
- The console's live sample is `fillSample(text, SAMPLE)`: the draft filled with made-up values, escaped like real
  ones, through `NoticeText` with links on. It lists every kind's key per language, a plural form as its own line.
- `FILLED_PLACEHOLDERS` is the list the test holds against the json; the same test checks that every bundled sentence
  in both languages uses only what its kind may, so a reset always lands on words the server would accept.

## Research

- **D1 - Activity, not Identity.** Notifications live in Activity; only emails needed Identity (it knows
  addresses).
- **D2 - the server stores edits, the bundle is the default.** A new kind needs no server change to have words, and
  an unreachable Activity leaves every notice readable.
- **D3 - escape the values, sanitise the words.** A product name is whatever a seller typed, and now that notices
  render HTML it must never become markup.

These three, and eight more the code makes (the placeholder map, append-only versions decided by `ON CONFLICT`, the
allow-list sanitised twice, links in the bell, the audit through Activity's own outbox, the refresh, the inline editor,
the tests), are written out with their rationale and rejected alternatives in [research.md](./research.md).

## Project Structure

### Documentation (this feature)

```text
specs/078-notification-wording/
├── spec.md                  # Feature specification (completed 2026-09-27)
├── plan.md                  # This file
├── research.md              # D1-D11
├── data-model.md            # notification_wording_versions; the placeholders section of notification-kinds.json
├── quickstart.md            # Validation scenarios
├── contracts/
│   └── http-api.md          # The six endpoints; the one message relied on (none changed)
├── checklists/
│   └── requirements.md      # Spec quality checklist
└── tasks.md                 # T001-T005 as merged, expanded
```

### Source code (touched by #162)

```text
server/src/BuildingBlocks/Ecommerce.Shared/Notifications/
├── notification-kinds.json                      # + "placeholders"
└── NotificationContract.cs                      # + Placeholders, PlaceholdersFor(kind)

server/src/Services/Activity/
├── Ecommerce.Activity.Domain/Entities/NotificationWordingVersion.cs                  # new
├── Ecommerce.Activity.Application/
│   ├── Common/Interfaces/INotificationWordingStore.cs                                # new - also INoticeSanitizer
│   └── Notifications/NotificationWordingFeatures.cs                                  # new - NotificationWording, requests, validator, handlers
├── Ecommerce.Activity.Infrastructure/
│   ├── Ecommerce.Activity.Infrastructure.csproj                                      # + HtmlSanitizer 9.2.1039
│   ├── DependencyInjection.cs                                                        # + store (scoped), sanitiser (singleton)
│   ├── NoticeSanitizer.cs                                                            # new
│   ├── Persistence/ActivityDbContext.cs                                              # + NotificationWordingVersions
│   ├── Persistence/Configurations/NotificationWordingVersionConfiguration.cs         # new
│   ├── Persistence/Repositories/NotificationWordingStore.cs                          # new - ON CONFLICT insert + stage + save
│   └── Migrations/20260926112005_AddNotificationWordingVersions.cs (+ Designer, snapshot)
└── Ecommerce.Activity.WebApi/
    ├── Controllers/NotificationWordingController.cs                                  # new
    └── Program.cs                                                                    # + AddAuditTrail("activity")

server/tests/Ecommerce.Activity.Tests/
├── ActivityTestFixture.cs                                                            # + store, sanitiser, test harness, audit trail
└── NotificationWordingTests.cs                                                       # new, 7 tests

client/
├── package.json, package-lock.json                                                   # + dompurify
└── src/
    ├── services/notification-wording/{index.ts,types.ts,index.test.ts}               # new
    ├── hooks/notification-wording/index.ts                                           # new
    ├── utils/notifications/{index.ts,index.test.ts}                                  # escaping, HTML, FILLED_PLACEHOLDERS
    ├── utils/notifications/{wording.ts,wording.test.ts}                              # new - applyWording, fillSample
    ├── components/shared/notice-text/{index.tsx,index.test.tsx}                      # new
    ├── components/shared/rich-text-editor/{index.tsx,one-line.ts}                    # variant="inline", token
    ├── components/layout/notification-bell/index.tsx                                 # NoticeText
    ├── pages/notifications/index.tsx                                                 # NoticeText
    ├── pages/admin-wording/{index.tsx,wording-editor.tsx,index.test.tsx}             # new
    ├── layouts/main-layout/index.tsx                                                 # useNotificationWording()
    ├── layouts/admin-layout/index.tsx                                                # menu link, admin only
    ├── routes/index.tsx                                                              # /admin/notifications, lazy
    ├── config/i18n/index.ts                                                          # bindI18nStore: 'added'
    ├── constants/query-keys/index.ts                                                 # three keys
    └── locales/{en,vi}/admin.json                                                    # the console's words

bruno/admin-users/                                                                    # seq 24-30, seven requests

docs/features/audit-and-notifications.md, docs/reference/{api,data-model}.md, docs/project/{timeline,backlog}.md,
docs/testing/testing-strategy.md, docs/overview/project-overview.md, docs/features/auth/db-design.md,
docs/features/catalog.md, CLAUDE.md
```

**Structure Decision**: Everything server-side sits in Activity, the service that already owns notifications and
their route. Nothing in the gateway, the contracts or any other service changed.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| | | |

## What this feature does not finish

- **Adding kinds from the console, per-person notification preferences, and sending notices by email** - out of scope
  in the spec.
- **Only `vi` and `en`.** The languages are a list in code (`NotificationWording.Languages`) on the server and the two
  locale files in the storefront.
- **An edit reaches an open page within the refresh, not at once.** 5 minutes, plus up to the 60 seconds a cache may
  keep the public answer; not measured.
- **No test makes the wording fetch fail.** That Activity down leaves the bundled words holds by construction
  (quickstart scenario 9).
- **The two allow-lists are configured separately** - Ganss.Xss in `NoticeSanitizer`, DOMPurify in `NoticeText` - and
  nothing but their tests keeps them alike.
- **A toast was lost after a save, reset or restore.** The console handed `onSuccess` callbacks to `mutate()`, and a
  save remounts the editor with a new version as its `key`, so the callback never ran. Found by the first Playwright
  run and fixed in specs/080 (#164) with `mutateAsync().then(...)`, with a test in `pages/admin-wording`.
- **The docs page's Messages table** (`docs/features/audit-and-notifications.md`) lists `AuditEntryRecorded` as
  published by Identity, Catalog, Inventory, Order and Payment; since this feature Activity publishes it too. Noted
  here, not changed (outside this directory).
