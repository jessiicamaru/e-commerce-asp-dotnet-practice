# Tasks: Product review before sale

- [X] T001 Catalog: review columns (default Approved), index, migration; `IsListed`, `Sellable` requires it
- [X] T002 Catalog: listing, lookup and pricing hide what is not approved; a seller's own list does not
- [X] T003 Catalog: create starts a seller's product Pending; queue, approve, reject, take down, resubmit with a guarded update and a stage
- [X] T004 Catalog: seller edits to text or images send an approved product back (six handlers)
- [X] T005 Tests: `ProductReviewTests` (7); mutation checks on the shelf filter, the sellable gate, and edit-sends-back
- [X] T006 Activity: `GET /api/audit/mine` for staff
- [X] T007 Bruno: the seller folder follows a product through pending, approved, renamed, rejected and resubmitted; 403s
- [X] T008 Client: review queue, moderator dashboard, seller badge and banner, wording; tests
- [X] T009 Run everything; verify-saga; docs; local demo seeder approves its sellers' products
