# Tasks: An administrator edits the emails

> Completed on 2026-09-27, after the feature merged (#161), from the code at that merge, the pull request and docs/features/email.md.

**Input**: [spec.md](./spec.md), [plan.md](./plan.md), [research.md](./research.md), [data-model.md](./data-model.md),
[contracts/http-api.md](./contracts/http-api.md)

**Tests**: included, and written first (T001) - the guarantees under test are a unique index racing eight saves and
an audit entry sharing a transaction, which only a real PostgreSQL can show (Principle V).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependencies)
- **[Story]**: US1 edit, US2 preview and test, US3 undo, US4 administrators only

## The original tasks

The five tasks the feature was built from, kept as written. T006-T032 below break them down by file, as the merge
shows them; every one is part of T001-T005, not extra work.

- [X] T001 Tests first, in `EmailTemplateTests`:
  - an unedited template sends the built-in words as HTML plus text;
  - a saved edit is what the next email says;
  - an unknown placeholder is refused, naming it;
  - `{link}` cannot be removed from the reset and confirmation emails;
  - scripts, handlers and `javascript:` are stripped on save;
  - a name is escaped;
  - a stale version is a 409, and two saves at once produce one version;
  - reset and restore;
  - the audit before and after;
  - preview and test send;
  - a language without an edit uses its own built-in words.
- [X] T002 The entity, configuration, migration, store, sanitiser, composer, handlers, controller and gateway route.
- [X] T003 Multipart sending through `SmtpEmailTransport`, and the dispatcher on the composer.
- [X] T004 Storefront: TipTap, the service, hooks, `/admin/emails`, the menu link and words. Vitest.
- [X] T005 Bruno (admin reads, saves, previews and resets; a moderator gets 403), the reference, mutation checks, the
  docs, and a live check in Mailpit.

---

## Phase 1: Setup

- [X] T006 Add `HtmlSanitizer` 9.2.1039 (Ganss.Xss) to `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Ecommerce.Identity.Infrastructure.csproj`
- [X] T007 [P] Add `@tiptap/react`, `@tiptap/starter-kit` and `@tiptap/pm` ^3.31.3 to `client/package.json` (and `client/package-lock.json`)

## Phase 2: Foundational (tests first, then the table and the rendering)

- [X] T008 Write `server/tests/Ecommerce.Identity.Tests/EmailTemplateTests.cs` (13 tests, T001's list) and register the store and sanitiser in `server/tests/Ecommerce.Identity.Tests/IdentityTestFixture.cs`; `FakeEmailTransport` records the HTML beside the text
- [X] T009 Create `EmailTemplateVersion` in `server/src/Services/Identity/Ecommerce.Identity.Domain/Entities/EmailTemplateVersion.cs`
- [X] T010 Create `EmailTemplateVersionConfiguration` in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Configurations/EmailTemplateVersionConfiguration.cs`: table `email_template_versions`, unique `(Template, Language, Version)`, CHECK `CK_email_template_versions_words`, and add the `DbSet` in `.../Persistence/ApplicationDbContext.cs`
- [X] T011 Generate the migration `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Migrations/20260926104309_AddEmailTemplateVersions.cs` (expand-only: one table)
- [X] T012 [P] Create `EmailHtml` in `server/src/Services/Identity/Ecommerce.Identity.Application/Email/EmailHtml.cs`: placeholders, plain text to HTML, the five-character encoder, filling HTML and the subject, HTML to text
- [X] T013 Extend `EmailTemplates` in `server/src/Services/Identity/Ecommerce.Identity.Application/Email/EmailTemplates.cs`: `Templates`, `Languages`, `PlaceholdersOf`, `RequiredOf`, `Default` as HTML, `SampleData`, `Render(..., edited)` returning `RenderedEmail(Subject, Html, Text)`

## Phase 3: US1 - Edit an email (P1)

- [X] T014 [US1] Declare `IEmailTemplateStore`, `IHtmlSanitizer`, `EmailComposer`, the commands, queries, responses and validators in `server/src/Services/Identity/Ecommerce.Identity.Application/Email/EmailTemplateEditing.cs`
- [X] T015 [US1] Implement `EmailTemplateHandlers` (list, versions, save) in the same file: sanitise, check placeholders by name and `{link}`, compare `expectedVersion`, add the version with its audit entry
- [X] T016 [P] [US1] Create `AllowListHtmlSanitizer` in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Email/AllowListHtmlSanitizer.cs`
- [X] T017 [P] [US1] Create `EmailTemplateStore` in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Email/EmailTemplateStore.cs`: `TryAddAsync` with `INSERT ... ON CONFLICT DO NOTHING`, staging and saving only on insert, one transaction inside the execution strategy
- [X] T018 [US1] Register the store and sanitiser in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/DependencyInjection.cs` and `EmailComposer` in `server/src/Services/Identity/Ecommerce.Identity.Application/DependencyInjection.cs`
- [X] T019 [US1] Change `IEmailTransport.SendAsync` to `(to, subject, text, html)` in `server/src/Services/Identity/Ecommerce.Identity.Application/Email/EmailInterfaces.cs`, and send `multipart/alternative` (text first, HTML in an inline-styled document) in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Email/SmtpEmailTransport.cs`
- [X] T020 [US1] Render through `EmailComposer` in `server/src/Services/Identity/Ecommerce.Identity.Application/Email/DispatchEmailsCommand.cs`, so every send sanitises the current version again
- [X] T021 [US1] Create `EmailTemplatesController` in `server/src/Services/Identity/Ecommerce.Identity.WebApi/Controllers/EmailTemplatesController.cs` at `api/email-templates`
- [X] T022 [P] [US1] Add `email-templates-route` and `email-templates-root-route` to `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`

## Phase 4: US2 - Preview and test (P1)

- [X] T023 [US2] Implement preview and test in `EmailTemplateHandlers` (`.../Application/Email/EmailTemplateEditing.cs`): sample data, the caller's address from the token, `[Test] ` subject, a refusing mail server as `DependencyUnavailableException` (503)

## Phase 5: US3 - Undo (P2)

- [X] T024 [US3] Implement reset (409 when already default) and restore (404 for a missing version; sanitised and checked again) in `.../Application/Email/EmailTemplateEditing.cs`, each a new version with its audit entry

## Phase 6: US4 - Administrators only (P1)

- [X] T025 [US4] `[Authorize(Roles = "Admin")]` on `EmailTemplatesController`; the storefront route behind `RequireRole role={['Admin']}` in `client/src/routes/index.tsx` and the menu item `adminOnly` in `client/src/layouts/admin-layout/index.tsx`

## Phase 7: Storefront

- [X] T026 [P] Create `EmailTemplates` in `client/src/services/email-template/index.ts` with its types in `client/src/services/email-template/types.ts`, tested in `client/src/services/email-template/index.test.ts`, and the query keys in `client/src/constants/query-keys/index.ts`
- [X] T027 [P] Create the hooks in `client/src/hooks/email-template/index.ts`
- [X] T028 [P] Create `RichTextEditor` in `client/src/components/shared/rich-text-editor/index.tsx` (TipTap, placeholders at the cursor, links to a placeholder) with `index.test.tsx`, and its styles in `client/src/index.css`
- [X] T029 Create `client/src/pages/admin-emails/index.tsx`, `email-editor.tsx` and `email-versions.tsx` (template and language tabs, preview in a sandboxed `iframe`, test, reset, versions with restore), tested in `client/src/pages/admin-emails/index.test.tsx` with the editor mocked as a textarea
- [X] T030 Lazy-load the page (`React.lazy` + `Suspense`) in `client/src/routes/index.tsx`, and add the words to `client/src/locales/{vi,en}/admin.json` and `common.json`

## Phase 8: Polish and evidence

- [X] T031 Add `bruno/admin-users/` 17-23 (403 for a moderator, the list, a placeholder refused by name, a preview without the script, a save, 409 on a stale version, a reset); run the mutation checks (seven, two survived at first and got their own tests); run the live check in Mailpit
- [X] T032 Update `docs/features/email.md` (Editing the words, data, tests, history), `docs/reference/` (generated), `docs/project/timeline.md`, `docs/project/backlog.md`, `docs/overview/project-overview.md`, `docs/testing/testing-strategy.md` and `CLAUDE.md`
- [X] T033 Merge through PR #161 (squash, 2026-09-26), `Refs #150` - the notification half follows in specs/078

## Dependencies

- T006-T013 before everything; T008 first, and red until T015.
- US1 (T014-T022) before US2 and US3, which reuse its sanitising, checking and version-adding.
- The storefront (T026-T030) needs the endpoints of T021-T022.
- T031-T032 last; T033 merges.

## Notes

- 33 tasks: the 5 original, 27 breaking them down by file, and T033 recording the merge.
- Found after the merge: a save, reset or restore changed the editor's `key` and its `mutate` callback's toast was
  lost; fixed in specs/080 (#164). See plan.md, "What this feature does not finish".
