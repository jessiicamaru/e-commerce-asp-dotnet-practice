# Tasks: Emails for what happens to people

- [X] T001 Eight template names in `EmailTemplate` (Shared), plus `ReadersLanguage`. In Identity's `EmailTemplates`:
  words in both languages, placeholders, required placeholders, sample data, values, and the console's order.
- [X] T002 `users.Language` and migration `AddUserLanguage` (it only adds a column). It is recorded at sign-up,
  sign-in and renewal (`RecordLanguageAsync`, one guarded `UPDATE`). `QueueEmailCommand` resolves
  `ReadersLanguage`.
- [X] T003 Senders:
  - Order: parcel shipped, whether the seller or the shop ships it; a cancellation, by the customer or by staff;
    a return accepted, refused or refunded (`ReturnParcel.Language`).
  - Catalog: back in stock, with `AddEmailSender`.
  - Identity: a lock or a ban, with `AddEmailSender`.
- [X] T004 Tests:
  - every template has words and renders its sample whole;
  - the shipped and lock wording;
  - lock and ban ask for one email each, and a refused lock asks for none;
  - the language is learnt, and nonsense changes nothing;
  - the reader's language is resolved;
  - shipping and cancelling ask for one email each in the order's language;
  - return decisions and the refund;
  - back in stock asks for one email per saver, and nothing for a product off the shelf;
  - storefront: every template is named in both languages.
- [ ] T005 Mutations, Bruno and Mailpit against the rebuilt stack, and the docs: email feature page,
  moderation page, CLAUDE.md, counts, timeline and backlog.
