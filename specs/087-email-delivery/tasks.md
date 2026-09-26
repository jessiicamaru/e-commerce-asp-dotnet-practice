# Tasks: Email delivery

- [X] T001 Identity:
  - `EmailDelivery` (the query, the retry, `MayRetry`);
  - `IOutgoingEmailRepository`'s `PageAsync`, `GetWithRecipientAsync` and `TryRetryAsync`;
  - `EmailsController`, and the gateway routes for `/api/emails`.
- [X] T002 `EmailDeliveryTests` (4):
  - listed with why and whom, never the data;
  - states and search;
  - a retry goes out once, is audited, and a second is 409;
  - a reset link is never retried, and an unknown id is 404.
- [X] T003 Storefront:
  - `services/outgoing-email`, `hooks/outgoing-email` and `pages/admin-email-delivery`;
  - the route, the nav entry, and the words in both languages;
  - tests for the service and the page.
- [X] T004 Three mutations (no guard, a reset link retried, the data returned), Bruno `admin-users/` 31-33 and a
  401 in `security-checks/`, and the docs: email, CLAUDE.md, decisions, reference, counts, timeline and backlog.
