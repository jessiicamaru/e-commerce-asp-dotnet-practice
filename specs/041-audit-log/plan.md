# Implementation Plan: An audit log of who did what

**Branch**: `041-audit-log` | **Spec**: [spec.md](spec.md) | [research](research.md)

## Technical Context

- **New service `Activity`** (Domain / Application / Infrastructure / WebApi, tests `Ecommerce.Activity.Tests`
  on 5440): `audit_entries` (id PK, category, action, actor id/email/role, subject type/id, summary,
  before/after `jsonb`, changes `jsonb`, service, occurred/recorded at; indexes on occurred, category,
  actor, subject). Consumer `RecordAuditEntryConsumer` → `RecordAuditEntryCommand`. Queries per D6.
- **Contracts**: `Activity/AuditEntryRecorded`.
- **Shared**: `Audit/IAuditTrail`, `AuditTrail`, `AuditCategory`, `AddAuditTrail(serviceName)`, snapshot +
  redaction.
- **Instrumented**: Identity (sign-in, refused sign-in, register, register-seller, shop rename, addresses),
  Catalog (product create/update/delete, prices, variants, images, categories, orphan reclaim), Inventory
  (stock set, expiry sweep), Order (placed, cancelled, parcel moves, received, auto-deliver, payout),
  Payment (charged/refused, refund).
- **Infra**: compose (db + app), gateway routes `/api/audit/**` and `/api/activity/health`, Dockerfile,
  CI (test DB, image list, publish lists), start-dev scripts, `.env.example`, slnx.
- **Client**: admin **Audit log** page (category tabs, filters, table) and entry dialog with the diff.

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | Activity owns its data; others publish, never read it. |
| II - Clean Architecture | Consumers in WebApi, commands in Application, SQL in Infrastructure. |
| III - Atomic, idempotent | Published before the one save (outbox); consumed idempotently by entry id. |
| IV - Identity from the token | Actor from `ICurrentUser`, never a body. |
| V - Evidence | Tests: diff, redaction, idempotence, filters, admin-only; per-service tests that an action records one entry; verify-saga. |

## Complexity Tracking

| Addition | Why | Simpler alternative rejected |
| :-- | :-- | :-- |
| A ninth service | Audit (and #87 notifications) are fed by every service and owned by none | Tables in each service: no single place to read, and cross-service queries |
| Audit + notifications in one service | Both are event sinks; halves the infra | Two services (user's choice was one) |
