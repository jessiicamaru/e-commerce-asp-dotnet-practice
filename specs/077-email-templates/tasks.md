# Tasks: An administrator edits the emails

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
