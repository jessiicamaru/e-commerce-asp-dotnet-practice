# Feature Specification: Behaviour promised but not held by a test

**Feature Branch**: `094-untested-promises` | **Created**: 2026-09-27 | **Issue**: #186

**Status**: Draft

**Input**: Issue #186 - "behaviour that is fixed or promised but not held by a test".

## Why

Rebuilding every design record to the standard of specs/001 (2026-09-27) turned up seven places where behaviour is
implemented, or claimed in a record, with no test holding it. None was a known defect; each could break silently. One
- the staff review list's paging - turned out to be a small defect as well: the rule every other paged query has was
missing.

| # | Where | What was not held |
| :-- | :-- | :-- |
| 1 | Storefront `email-editor`, `email-versions`, `wording-editor` (specs/080) | the **reset** and **restore** toasts after the editor remounts - only **save** had a test |
| 2 | Storefront notice wording (specs/078) | the wording fetch **failing** leaves the bundled words |
| 3 | Order (specs/012) | a tax rate changed in configuration **does not change an existing order** |
| 4 | Orchestrator (specs/022) | the saga relays the order's **currency** into `ProcessPaymentCommand` |
| 5 | Identity (specs/087) | two administrators retrying one failed email **at the same moment** |
| 6 | Identity (specs/044) | `SellerRegisteredEvent.ShopName` asserted; two applications **raced** |
| 7 | Catalog (specs/046) | the staff review list's **paging** (`pageSize` unbounded) - a fix and its test |

## User Scenarios & Testing *(mandatory)*

### US1 - Every row has a test that fails when its behaviour is reverted (Priority: P1)

For each row, a test that passes today and fails when the behaviour it describes is undone - shown by a mutation.

**Why this priority**: It is the whole issue. A test that cannot fail proves nothing (Principle V).

**Independent Test**: For each row, apply the mutation in [quickstart.md](quickstart.md) and see the named test go red.

**Acceptance Scenarios**:

1. **Row 1** - **Given** an editor whose reset or restore is still in flight, **When** the page unmounts and the server
   then answers, **Then** the toast is still shown (four tests: email reset, email restore, wording reset, wording
   restore).
2. **Row 2** - **Given** the wording endpoint failing, **When** the storefront has settled, **Then** a notice reads its
   bundled words; and **given** it answering with an edit, the edit is laid over them (the control).
3. **Row 3** - **Given** an order placed at 10%, **When** the service restarts with 25% and the order is read,
   **Then** it still says 10% and the tax it charged, while a new checkout says 25%.
4. **Row 4** - **Given** an order submitted in USD with a variant, **Then** the saga's `ReserveInventoryCommand` carries
   the variant and its `ProcessPaymentCommand` the amount **and** `USD`.
5. **Row 5** - **Given** one failed email, **When** five retries arrive at once, **Then** one succeeds, four are 409,
   and the person receives the email once.
6. **Row 6** - **Given** an approved application, **Then** `SellerRegisteredEvent.ShopName` is the shop's name; **given**
   five simultaneous applications from one person, **Then** one is waiting and four are 409.
7. **Row 7** - **Given** the staff review list, **When** asked for page 0, or a page of 0, 51 or 100,000, **Then** 400;
   a page of 50 is allowed.

---

### US2 - The one missing rule is added (Priority: P1)

`GetReviewsForStaffQuery` is validated like every other paged Catalog query: page ≥ 1, size 1-50.

**Why this priority**: An administrator's request for 100,000 hidden reviews was one query and one response; the rest
of Catalog refuses that.

**Independent Test**: Row 7's test, red before the validator.

**Acceptance Scenarios**: as row 7 above.

### Edge Cases

- **Row 2's guards are two.** The hook applies wording only when there is data, and `applyWording` starts from the
  bundle and deep-merges. Breaking either alone does not blank a notice; the mutation that turns the test red breaks
  both (research D3). Recorded so nobody reads the test as depending on one guard.
- **Row 3 is a restart, not a live change.** `ConfiguredTaxRates` reads configuration once; the test builds the same
  service again with another rate against the same database.
- **Row 4 also holds the variant relay** - the specs/020 gotcha ("a service that only relays a contract must be rebuilt
  when that contract grows") had no test either.
- **Row 5's losers.** Each gets the 409 of the guarded `UPDATE`; none is sent.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Each of the seven rows has at least one automated test in the project that owns the behaviour.
- **FR-002**: Each test is shown to fail under a mutation that reverts its behaviour; the mutation is recorded.
- **FR-003**: `GetReviewsForStaffQueryValidator` refuses a page number below 1 and a page size outside 1-50.
- **FR-004**: No other production behaviour changes. The Order test fixture gains `RestartedWith(...)`, test code only.

### Key Entities

None new.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 14 new or strengthened tests across Identity, Order, Orchestrator, Catalog and the storefront, all green.
- **SC-002**: Every row's mutation turns its test red (quickstart table).
- **SC-003**: The affected suites pass in full: Identity, Order, Orchestrator, Catalog; storefront tests, lint and types.

## Decision

**Row 7 is fixed, not only tested**: a test of "unbounded" would have to assert the defect. The rule is the one the rest
of Catalog already uses (1-50), not a new number. Recorded as decided on the user's behalf
([research.md](research.md) D1).

## Assumptions

- A mutation is applied by hand, run, and reverted; the source is identical afterwards (`git status` clean for it).

## Out of scope

- Behaviour not listed in #186.
