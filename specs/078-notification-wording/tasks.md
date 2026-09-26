# Tasks: An administrator rewords the notifications

- [ ] T001 Tests first, in `NotificationWordingTests`:
  - a save is what the storefront is given, in that language only;
  - an unknown placeholder is refused by name;
  - plural keys, and unknown keys and languages are 404s;
  - sanitising, and links to the web or the shop only;
  - a stale version is a 409, and the store gives a number once;
  - reset and restore, audited;
  - the overview with each kind's placeholders.
- [ ] T002 The placeholders section in `notification-kinds.json`, and `NotificationContract.PlaceholdersFor`.
- [ ] T003 The entity, configuration, migration, store, sanitiser, handlers, controller, and the audit trail in
  Activity.
- [ ] T004 Storefront: wording applied over the bundle, notices rendered as sanitised rich text with escaped values,
  the inline editor, `/admin/notifications`, the menu link and words. Vitest, including that `describeNotification`
  and the json declare the same placeholders.
- [ ] T005 Bruno (the public read, an admin save, a refusal by name, a moderator's 403, a reset), the reference,
  mutation checks, the docs, and a live check that the bell shows reworded words.
