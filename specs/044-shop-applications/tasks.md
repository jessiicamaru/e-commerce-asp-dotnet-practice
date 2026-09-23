# Tasks: Shop applications

- [X] T001 Identity: `ShopApplication` entity, configuration (one-pending index), migration
- [X] T002 Identity: `register-seller` creates a customer and a pending application
- [X] T003 Identity: apply / mine / queue / approve / reject, guarded decision with a stage; controller
- [X] T004 Tests: `ShopApplicationTests` (no shop before approval, approval does it all once, five racing approvals, reject then re-apply, rules, queue order); seller tests go through approval
- [X] T005 Gateway route; Bruno seller folder applies, is refused, is approved, signs in as a seller; 401 without a token
- [X] T006 Client: `/open-shop`, `/admin/shops`, menu entry, session refresh, wording; tests
- [X] T007 Run everything; verify-saga; docs; local demo seeder approves its sellers
