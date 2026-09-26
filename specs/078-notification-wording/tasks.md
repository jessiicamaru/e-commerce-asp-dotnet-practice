---
description: "Task list for An administrator rewords the notifications"
---

# Tasks: An administrator rewords the notifications

> Completed on 2026-09-27, after the feature merged (#162), from the code at that merge, the pull request and docs/features/audit-and-notifications.md.

**Input**: Design documents from `/specs/078-notification-wording/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included. The guarantee that two saves store one version belongs to PostgreSQL's unique index, so it is
tested against a real database (constitution Principle V); the escaping is the one thing between a seller's product
name and a reader's page, so it has a test that a mutation proved bites.

## The tasks as merged

The five tasks the feature was built from, kept exactly as they were ticked in #162. The phases below break them into
the files each touched; every task there is part of one of these five.

- [X] T001 Tests first, in `NotificationWordingTests`:
  - a save is what the storefront is given, in that language only;
  - an unknown placeholder is refused by name;
  - plural keys, and unknown keys and languages are 404s;
  - sanitising, and links to the web or the shop only;
  - a stale version is a 409, and the store gives a number once;
  - reset and restore, audited;
  - the overview with each kind's placeholders.
- [X] T002 The placeholders section in `notification-kinds.json`, and `NotificationContract.PlaceholdersFor`.
- [X] T003 The entity, configuration, migration, store, sanitiser, handlers, controller, and the audit trail in
  Activity.
- [X] T004 Storefront: wording applied over the bundle, notices rendered as sanitised rich text with escaped values,
  the inline editor, `/admin/notifications`, the menu link and words. Vitest, including that `describeNotification`
  and the json declare the same placeholders.
- [X] T005 Bruno (the public read, an admin save, a refusal by name, a moderator's 403, a reset), the reference,
  mutation checks, the docs, and a live check that the bell shows reworded words.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 reword, US2 safe by construction, US3 default and undo, US4 administrators only

---

## Phase 1: Setup

- [X] T006 [P] Add `HtmlSanitizer` 9.2.1039 to `server/src/Services/Activity/Ecommerce.Activity.Infrastructure/Ecommerce.Activity.Infrastructure.csproj` (part of T003)
- [X] T007 [P] Add `dompurify` to `client/package.json` and `client/package-lock.json` (part of T004)

---

## Phase 2: Foundational - the shared declaration (T002)

- [X] T008 Add the `placeholders` section (`order`, `total`, `amount`, `tracking`, `shop`, `by`, `product`, `reason`, `rating`, `count`, `until`, each with its data keys) to `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/notification-kinds.json`
- [X] T009 Add `Placeholders` and `PlaceholdersFor(kind)` - required **and** optional keys count - to `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/NotificationContract.cs`

---

## Phase 3: Tests first (T001)

- [X] T010 [US1] [US2] [US3] Write `server/tests/Ecommerce.Activity.Tests/NotificationWordingTests.cs` (7 tests, real PostgreSQL on 5440), each test starting and ending with no saved wording
- [X] T011 Register the store, the sanitiser, `AddMassTransitTestHarness()` and `AddAuditTrail("activity")` in `server/tests/Ecommerce.Activity.Tests/ActivityTestFixture.cs`, so the published audit entries can be read

---

## Phase 4: User Story 1 and 3 - versions in Activity (T003)

- [X] T012 [P] [US1] Create `NotificationWordingVersion` in `server/src/Services/Activity/Ecommerce.Activity.Domain/Entities/NotificationWordingVersion.cs` (v7 id; `Text` null when `IsDefault`)
- [X] T013 [P] [US1] Create `NotificationWordingVersionConfiguration` in `server/src/Services/Activity/Ecommerce.Activity.Infrastructure/Persistence/Configurations/NotificationWordingVersionConfiguration.cs`: table `notification_wording_versions`, `ValueGeneratedNever()`, unique `(Key, Language, Version)`, CHECK `"IsDefault" OR "Text" IS NOT NULL`
- [X] T014 [US1] Add `NotificationWordingVersions` to `server/src/Services/Activity/Ecommerce.Activity.Infrastructure/Persistence/ActivityDbContext.cs` and generate `server/src/Services/Activity/Ecommerce.Activity.Infrastructure/Migrations/20260926112005_AddNotificationWordingVersions.cs` (a table and an index only)
- [X] T015 [US1] Declare `INotificationWordingStore` and `INoticeSanitizer` in `server/src/Services/Activity/Ecommerce.Activity.Application/Common/Interfaces/INotificationWordingStore.cs`
- [X] T016 [US3] Implement `NotificationWordingStore` in `server/src/Services/Activity/Ecommerce.Activity.Infrastructure/Persistence/Repositories/NotificationWordingStore.cs`: current, current-of-all, history, one version, and `TryAddAsync` - `INSERT ... ON CONFLICT DO NOTHING`, and only when it inserted, the audit `stage` and one save, in one transaction inside the execution strategy
- [X] T017 [US1] [US2] [US3] Implement `NotificationWording` (keys, plural suffixes, placeholders, bad links), the queries, commands, validator and `NotificationWordingHandlers` in `server/src/Services/Activity/Ecommerce.Activity.Application/Notifications/NotificationWordingFeatures.cs` - a stale `expectedVersion` 409, reset of a default 409, a restored version checked again
- [X] T018 [US4] Implement `NotificationWordingController` in `server/src/Services/Activity/Ecommerce.Activity.WebApi/Controllers/NotificationWordingController.cs`: the public `GET` `[AllowAnonymous]` with a 60-second `ResponseCache`, everything else `[Authorize(Roles = "Admin")]`
- [X] T019 [US3] Register `AddAuditTrail("activity")` in `server/src/Services/Activity/Ecommerce.Activity.WebApi/Program.cs`, and the store (scoped) and sanitiser (singleton) in `server/src/Services/Activity/Ecommerce.Activity.Infrastructure/DependencyInjection.cs`

---

## Phase 5: User Story 2 - safe by construction (T003, T004)

- [X] T020 [P] [US2] Implement `NoticeSanitizer` in `server/src/Services/Activity/Ecommerce.Activity.Infrastructure/NoticeSanitizer.cs`: `strong`, `b`, `em`, `i`, `u`, `a[href]`, `http`/`https`, `KeepChildNodes`, and a script's or style's text dropped with it
- [X] T021 [P] [US2] Escape every value in `describeNotification` and return HTML; export `escapeHtml` and `FILLED_PLACEHOLDERS` in `client/src/utils/notifications/index.ts`
- [X] T022 [P] [US2] Create `NoticeText` (DOMPurify with the server's allow-list, `links` off by default, only `http(s)://` and single-slash addresses) in `client/src/components/shared/notice-text/index.tsx`, with `index.test.tsx`
- [X] T023 [US2] Show notices through `NoticeText` in `client/src/components/layout/notification-bell/index.tsx` and `client/src/pages/notifications/index.tsx`
- [X] T024 [US2] Test in `client/src/utils/notifications/index.test.ts` that `describeNotification` fills exactly the placeholders the json declares, that every bundled sentence in both languages uses only what its kind may, and that values are escaped

---

## Phase 6: User Story 1 - the storefront lays the edits over the bundle (T004)

- [X] T025 [P] [US1] Create `NotificationWording` (current, overview, versions, save, reset, restore) in `client/src/services/notification-wording/index.ts`, types in `types.ts`, and `index.test.ts`
- [X] T026 [P] [US1] Create `applyWording` (bundle first, then edits) and `fillSample` in `client/src/utils/notifications/wording.ts`, with `wording.test.ts`
- [X] T027 [US1] Create `useNotificationWording` (5-minute refresh, `retry: false`), `useWordingOverview`, `useWordingVersions` and `useWordingChanges` in `client/src/hooks/notification-wording/index.ts`, with three keys in `client/src/constants/query-keys/index.ts`
- [X] T028 [US1] Call `useNotificationWording()` in `client/src/layouts/main-layout/index.tsx`, and set `react.bindI18nStore: 'added'` in `client/src/config/i18n/index.ts`

---

## Phase 7: User Story 1, 3 and 4 - the console (T004)

- [X] T029 [P] [US1] Add `variant="inline"` and `token` to `client/src/components/shared/rich-text-editor/index.tsx`, with `oneLine` in `client/src/components/shared/rich-text-editor/one-line.ts`
- [X] T030 [US1] [US3] Create `AdminWordingPage` and `WordingEditor` (every kind's key per language, plural forms one by one, the inline editor with `{{name}}` placeholders, a live sample, save, reset, versions with restore) in `client/src/pages/admin-wording/index.tsx` and `wording-editor.tsx`, with `index.test.tsx`
- [X] T031 [US4] Route `/admin/notifications` lazily behind `RequireRole role={['Admin']}` in `client/src/routes/index.tsx`, and add the admin-only menu link in `client/src/layouts/admin-layout/index.tsx`
- [X] T032 [P] [US1] Add the console's words to `client/src/locales/en/admin.json` and `client/src/locales/vi/admin.json`

---

## Phase 8: Polish (T005)

- [X] T033 [P] [US1] [US2] [US4] Add Bruno requests `bruno/admin-users/` seq 24-30: the public read, a moderator's 403, the overview, a refusal naming `{{total}}`, a reword with the script stripped, the new words read back in English only, and a reset
- [X] T034 [P] Regenerate `docs/reference/api.md` and `docs/reference/data-model.md` with `python docs/tools/generate_reference.py`
- [X] T035 [P] Add *Rewording the notices* and the data, API, storefront, tests and mutation checks to `docs/features/audit-and-notifications.md`; update `CLAUDE.md`, `docs/project/timeline.md`, `docs/project/backlog.md` (#150 fixed), `docs/testing/testing-strategy.md`, `docs/overview/project-overview.md`, and the three data-model anchors whose table counts had drifted (`docs/features/auth/db-design.md`, `docs/features/catalog.md`, and Activity's in `docs/features/audit-and-notifications.md`)
- [X] T036 Run the mutation checks - accepting any key, not checking placeholders, not checking links, not sanitising, no `ON CONFLICT`, no stale check, showing defaults as edits, ignoring optional keys (survived until the `ParcelShipped` assertion), and the client's escaping - each caught
- [X] T037 Run the suites: `Ecommerce.Activity.Tests` 35/35, the storefront 450/450 in 81 files with lint, `tsc -b` and the build clean, Bruno 263/263 requests and 428/428 tests through the rebuilt storefront container; and look at the bell showing reworded words
- [X] T038 Merge through #162 (2026-09-26, `5a136f0`), closing #150

---

## Dependencies & Execution Order

- **Setup (T006-T007)** and **the declaration (T008-T009)** first: the handlers and the client test both read the
  placeholders.
- **Tests (T010-T011)** before the Activity implementation they describe.
- **Activity (T012-T020)** before the storefront's service and console (T025-T031), which call its endpoints; the
  storefront's escaping and `NoticeText` (T021-T024) need only the json.
- **Polish (T033-T038)** last.

## Notes

- 38 tasks: the 5 as merged, and 33 that break them into files.
- One later change to this feature's code, outside it: specs/080 (#164) replaced the console's `mutate(..., {
  onSuccess })` with `mutateAsync().then(...)` in `client/src/pages/admin-wording/wording-editor.tsx`, because a save
  remounts the editor and the toast was lost. See [plan.md](./plan.md), *What this feature does not finish*.
