# Research: Every audit action has words, and the status page lists every service

## D1 - Find the actions in the server's source

**Decision**: A client test parses every `audit.RecordAsync(` call in `server/src` and takes its second argument.

**Rationale**: The actions are string literals at about a hundred call sites in six services; there is no list to read. `notification-kinds.json` solves the same problem for notices by declaring a list that both sides test, but for the audit log the call sites already ARE the declaration - a second list would be one more place to forget. Literals and ternaries are read directly; the five files that pass a variable are declared in the test with the actions they produce, each checked to appear in that file.

**Alternatives rejected**: A shared `audit-actions.json` with server tests capturing entries at run time - every service's tests would need a capturing trail, for a property a source scan holds already; a constants class - a refactor of a hundred call sites.

## D2 - Hold the status page to the gateway

**Decision**: A test reads `appsettings.json`'s `*-health-route` routes and compares them with `HEALTH_SERVICES`.

**Rationale**: The page went stale twice (orchestrator, Activity) because nothing compared it with the routes.

**Alternatives rejected**: Asking the gateway for its routes at run time - it does not expose them.
