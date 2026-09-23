# Tasks: An audit log of who did what

- [X] T001 Contracts `AuditEntryRecorded`; Shared `Audit/*` (+ Contracts reference); redaction tests
- [X] T002 Activity service skeleton (4 projects, Program, health, db, migrations) + test project
- [X] T003 Tests first: record + diff + redaction + idempotence + filters + summary - tests/Ecommerce.Activity.Tests
- [X] T004 Activity: entity, config, `RecordAuditEntryCommand` (diff), queries, controller (Admin), consumer
- [X] T005 Instrument Identity; tests
- [X] T006 Instrument Catalog; tests
- [X] T007 Instrument Inventory; tests
- [X] T008 Instrument Order; tests
- [X] T009 Instrument Payment; tests
- [X] T010 Infra: compose, gateway, Dockerfile, CI, start-dev, env example, slnx
- [X] T011 Client: admin Audit log page + entry dialog; vi/en; tests
- [X] T012 Bruno: audit list (admin 200, customer 403); verify-saga; docs; mutation checks

> The Activity service's 24 tests were written with its code; the diff test found a real defect - a JSON
> null leaf crashed the comparison - before anything shipped. Per-service audit tests: Identity 3, Catalog 2,
> Inventory 2, Payment 1, Order 4, each asserting one entry per action, the actor, and none for a refused
> change. End to end: Bruno reads the order it followed back from `/api/audit` (placed, prepared, shipped,
> received - once each), 403 for a customer, 401 anonymous; verify-saga.sh passes; the admin page shows
> the diff old → new.
> Mutations, one at a time, each turning a test red: redaction off, the `ON CONFLICT` removed, a parcel move
> saved without its entry, a refused sign-in recorded under another name.
