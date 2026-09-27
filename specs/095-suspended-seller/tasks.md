---
description: "Task list for A banned seller's shop is closed"
---

# Tasks: A banned seller's shop is closed

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written first - Identity's two failed before the fix; Catalog's could not compile until the command
existed.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: The message

- [X] T001 `SellerSuspensionChangedEvent` in `server/src/BuildingBlocks/Ecommerce.Contracts/Identity/`

## Phase 2: Catalog (US1, US2)

- [X] T002 [US1] [US2] `SellerSuspensionTests` (5) in `server/tests/Ecommerce.Catalog.Tests/`
- [X] T003 [US1] `Product.SellerSuspended`, `Seller.Suspended` / `SuspensionChangedAt`; mappings; migration `SellerSuspension`
- [X] T004 [US1] `OnShelf` and the listing's SQL include the suspension
- [X] T005 [US1] [US2] `ISellerRepository.TryRecordSuspensionAsync` (guarded upsert + products, one transaction); empty names skipped by `GetNamesAsync`
- [X] T006 [US2] `RecordSellerSuspensionCommand`; reinstatement tells savers (`GetOnSaleBySellerAsync`, `SavedProductNotices`)
- [X] T007 `SellerSuspensionChangedConsumer`, registered in `Program.cs`

## Phase 3: Identity (US1, US2, US3)

- [X] T008 [US1] [US2] Tests in `ModerationTests.cs`: ban closes and lift reopens a seller's shop; a customer's ban and a seller's lock announce nothing - red before
- [X] T009 [US3] `A_banned_applicant_is_not_given_a_shop` in `ShopApplicationTests.cs` - red before
- [X] T010 [US1] [US2] Ban and lift publish the message for a seller (`UserAdministration.cs`)
- [X] T011 [US3] Approval refuses a banned applicant (`ShopApplicationFeatures.cs`)

## Phase 4: Storefront (US4)

- [X] T012 [P] [US4] "Banned · shop closed" on `/admin/users`, en and vi, with its test

## Phase 5: Verification and docs

- [X] T013 Mutations (quickstart Scenario 3) - each red; Identity 188/188, Catalog 226/226, admin-users 10/10
- [X] T014 [P] Bruno `seller/` seq 18-22: ban, 404, lift, 200, sign in again
- [X] T015 Rebuilt Identity and Catalog; Bruno through the gateway
- [X] T016 Docs: `docs/features/marketplace.md`, `docs/features/moderation-and-staff.md`, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [ ] T017 Merged as #202, closing #193
