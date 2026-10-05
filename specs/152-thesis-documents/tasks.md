---
description: "Task list for The report's top-down documents"
---

# Tasks: The report's top-down documents

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: a link check; numbers against their sources.

- [x] T001 `docs/overview/architecture-overview.md`: one diagram, the request and message paths, links down
- [x] T002 `docs/guides/deployment.md`: from a bare server to a running shop, and what is not exercised yet
- [x] T003 `docs/testing/evaluation.md`: tests, guarantees, latency, resilience, security, defects found, limits
- [x] T004 `docs/guides/demo-script.md`: start, seed, every role, the operational views
- [x] T005 `docs/README.md` links them; CLAUDE.md's test counts; the link check
- [x] T006 Merged, closes #295 - #327

## Evidence

- Four documents, linked under "Read first" in `docs/README.md`:
  - [architecture overview](../../docs/overview/architecture-overview.md): one Mermaid diagram of the 8 services,
    both apps, the gateway, Caddy, RabbitMQ, SeaweedFS, SMTP, VNPay, Seq, Prometheus and Grafana; the six gRPC edges
    as registered in code (`AddGrpcClient`); the request path of placing an order; the saga's messages, every name
    checked against `Ecommerce.Contracts`;
  - [deployment](../../docs/guides/deployment.md): eight steps from a bare server, with what is not exercised yet;
  - [evaluation](../../docs/testing/evaluation.md): tests, mutations, the race, performance, resilience, security,
    four defects found by measurement, limitations;
  - [demo script](../../docs/guides/demo-script.md): every role, every page a route in one of the two routers.
- **Link check**: 80 relative links across the four, all resolving. The first pass found 7 wrong spec directory names,
  which were fixed.
- **Numbers against their sources**:
  - test counts from CI's build job (research D2) and `vitest run` (742 in 133 files);
  - the race, checkout and browse figures from `load-test-results.md`;
  - the fault table from `resilience-results.md`;
  - the scans from `security-scanning.md`;
  - mutations: 114 of the 151 records (`grep -li mutation specs/*/tasks.md`);
  - +198 paid and the 616-message outbox from specs/148's evidence.
- One sentence was removed while checking: a claim about tests found hollow by mutation, which I could not source.
- CLAUDE.md's test counts were brought up to date: Catalog 268, Identity 289, Activity 52, ApiGateway 22.
