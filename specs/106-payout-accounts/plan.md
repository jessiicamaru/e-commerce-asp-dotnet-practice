# Implementation Plan: Sellers say where their payouts go

**Branch**: `106-payout-accounts` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #213

## Summary

A seller saves a payout account in Identity. They read it back masked, and a change emails them and is audited with
the number masked. Administrators read the full accounts to make transfers. Recording a payout in Order asks Identity
for the account over gRPC with the administrator's token, refuses when there is none, and freezes the bank, the holder
and the last four digits onto the payout.

## Technical Context

- **Identity**:
  - `SellerPayoutAccount` entity, its configuration, the `DbSet` and migration `AddSellerPayoutAccounts`.
  - `Sellers/PayoutAccounts.cs` (queries, command, validator).
  - `SellersController` (three routes).
  - `Grpc/PayoutAccountsService.cs`, mapped in `Program.cs`.
  - The `PayoutAccountChanged` email in `EmailTemplates`.
- **Contracts.Grpc**: `payout_accounts.proto`.
- **Shared**: `EmailTemplate.PayoutAccountChanged`.
- **Order**:
  - `IPayoutAccounts` and `GrpcPayoutAccounts` (forwarding the token, as `GrpcAddressReader` does).
  - `Payout` columns and migration `AddPayoutDestination`.
  - `RecordPayoutCommandHandler`, and `PayoutRepository` (the claim writes the destination).
  - Payout responses.
- **Storefront**:
  - `Seller.payoutAccount` and `setPayoutAccount`, `Admin.payoutAccounts`.
  - A payout-account card on `/shop/payouts`.
  - Accounts on `/admin/payouts`.
  - Words, and the email label in `admin.json`.

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: EF Core 10 + Npgsql, Grpc.AspNetCore and Grpc.Net.Client, MassTransit outbox (audit, email);
TanStack Query

**Storage**: 1 table in `ecommerce_identity_db`; 3 columns in `ecommerce_order_db`

**Testing**: xUnit against real PostgreSQL; Vitest; Bruno

**Target Platform**: Identity (5056, gRPC 6056), Order (5059), the storefront

**Performance Goals**: one gRPC call per payout recorded

**Constraints**: the full number appears only in Identity and its Admin route; never in Order, audit, email or the
broker

**Scale/Scope**: 1 table, 3 routes, 1 gRPC call, 1 email, 2 pages

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass, with a new synchronous edge** (Order to Identity, for recording payouts only). It is justified in research D2: the alternative puts secrets on the broker. Checkout is untouched, and an unreachable Identity refuses a payout rather than recording one without a destination. |
| **II. Clean Architecture Layering** | **Pass.** `IPayoutAccounts` is in Application and the gRPC client in Infrastructure, as for addresses. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The account write, its audit entry and its email commit in one save. The payout's destination is written by the same statement that claims the parts. |
| **IV. Identity Comes From the Token** | **Pass.** The seller's routes take no seller id. The gRPC read is authorized by the forwarded administrator token. |
| **V. Evidence Over Assumption** | **Planned.** Identity and Order tests against real databases, mutations, Bruno and Mailpit through rebuilt containers; recorded in `tasks.md`. |

**Post-design re-check**: to be done once implemented; results go in `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/106-payout-accounts/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D4
├── data-model.md        # The table, the payout columns, the email
├── quickstart.md
├── contracts/
│   ├── http-api.md      # the three Identity routes, the payout change
│   └── grpc.md          # PayoutAccounts
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

As in Technical Context. Docs: `docs/features/marketplace.md`, `docs/features/email.md`,
`docs/architecture/service-to-service-communication.md` (the new edge), CLAUDE.md, backlog, timeline, and a run of
`generate_reference.py`.

## Complexity Tracking

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| A new synchronous call, Order to Identity, when recording a payout | The payout must record its destination at the moment it is paid | A read model would copy bank details into outbox rows and broker queues; a client-supplied destination is not the server deciding |
