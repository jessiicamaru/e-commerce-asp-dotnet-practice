# Tasks: The Orchestrator answers /health

- [X] T001 Add the health checks and `/health` in `server/src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/Program.cs`.
- [X] T002 Gateway cluster and route (`appsettings.json`), and the compose override. Re-enable the compose health check.
- [X] T003 The CI saga job waits for it. `verify-saga.sh` reports its health on a stall.
- [X] T004 A test, the live check up and down through the gateway, and docs: CLAUDE.md (the recorded disagreement is resolved) and the architecture pages.
