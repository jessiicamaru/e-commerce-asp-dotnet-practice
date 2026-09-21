# Feature Specification: The Checkout Flow Is Verified End to End

**Feature Branch**: `007-saga-e2e-verification`

**Created**: 2026-09-21

**Status**: Draft

**Input**: An automated end-to-end verification of the checkout saga, run in CI on every change.

## Why this exists

This system's reason for existing is a single cross-service promise: an order is placed, stock is
held, payment is taken, the order completes, and the stock is gone for good. Six services have to
agree for that sentence to be true.

**Nothing checks it.** There are 61 automated tests and every one of them exercises a single
service. They are good tests — they run against a real database and they catch overselling,
re-settlement and lost updates. None of them can see the gaps *between* services, and that is where
this system's characteristic failures live.

This is not hypothetical. Two services each declared a message handler with the same class name, so
both bound to the same queue and competed for the same event instead of each receiving it. An order
settled while its stock stayed held — the exact failure the whole design exists to prevent. **All 15
of that service's tests were green throughout**, and stayed green, because the defect was not inside
any service. It was found by a person, by hand, after the fact.

The defence against that class of failure today is a paragraph in a documentation file asking the
next person to remember. That is not a defence.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A change that breaks checkout is caught before it is accepted (Priority: P1)

Someone proposes a change. Without anybody running anything by hand, the pipeline places a real
order through the public interface, follows it until it reaches a final state, and confirms both
that the order completed **and** that the stock is actually gone.

**Why this priority**: It is the MVP, and it is the one with proven value — this exact check would
have caught the failure described above, which every existing test missed. It also establishes
everything the second story needs.

**Independent Test**: Place an order for a known quantity, wait for it to settle, and compare the
stock before and after. Delivers value alone: from this story onward, no change can silently break
the successful checkout path.

**Acceptance Scenarios**:

1. **Given** a catalogue item with a known quantity in stock, **When** an order for part of that
   quantity is placed by an authenticated customer, **Then** the order reaches a completed state and
   the remaining stock is lower by exactly the amount ordered.
2. **Given** the flow has completed, **When** the stock is inspected, **Then** none of it is still
   being held — a completed order releases its hold by consuming it, not by keeping it.
3. **Given** any step of the flow never happens, **When** the check gives up waiting, **Then** it
   reports which state the order was last observed in and what it was waiting for, rather than only
   that it timed out.

---

### User Story 2 - The failure path still puts the stock back (Priority: P1)

The same order is placed while payment is configured to refuse. The order must end in a failed
state, and every unit that was held for it must be back on the shelf.

**Why this priority**: Equal first, for the opposite reason to US1. The successful path is exercised
constantly — by hand, in demos, in every casual check. **Nobody exercises the compensation path**,
which makes it the one most likely to have rotted without anybody noticing. Stock stranded by a
failed order is invisible until the shelf is empty and nothing explains why.

**Independent Test**: Place an order against a refusing payment configuration and compare the stock
before and after. It must be unchanged — not merely "not sold", but actually released.

**Acceptance Scenarios**:

1. **Given** a catalogue item with a known quantity, **When** an order is placed and payment
   refuses, **Then** the order reaches a failed state.
2. **Given** that failed order, **When** the stock is inspected, **Then** the available quantity is
   exactly what it was before the order — the compensation returned the held units rather than
   leaving them held.
3. **Given** a failure happens at the reservation step instead of the payment step, **When** the
   order settles, **Then** it also ends failed. Both failure routes lead to the same outcome, and
   the check must not assume only one of them exists.

---

### User Story 3 - The result can be trusted (Priority: P2)

The check states what it verified. When it could not run, it says so rather than staying quiet, and
it has been demonstrated capable of failing.

**Why this priority**: Third because US1 and US2 deliver the protection. But protection that cannot
fail is not protection, and **this repository has already shipped exactly that**: a check that
reported clean on an artifact that provably leaked, because it was examining the wrong thing. It was
believed for five days. A green tick is not evidence; a green tick from a check proven able to go
red is.

**Independent Test**: Deliberately break the flow, confirm the check fails and says what was wrong.
Then remove a prerequisite and confirm it reports being unable to run rather than reporting success.

**Acceptance Scenarios**:

1. **Given** a deliberately broken flow, **When** the check runs, **Then** it fails and names which
   guarantee was violated.
2. **Given** a prerequisite is missing, **When** the check runs, **Then** it reports that it could
   not run, and that report is distinguishable at a glance from a pass.
3. **Given** a normal run, **When** it finishes, **Then** it states which scenarios it exercised, so
   that exercising none is visibly different from exercising all of them.

---

### Edge Cases

- **The flow is asynchronous, so "it has not happened yet" and "it will never happen" look
  identical.** The check has to wait, and a wait that is too short turns a slow shared machine into
  a false alarm. A false alarm that recurs teaches people to re-run the pipeline without reading it,
  which costs more than the check is worth.
- **A timeout must not be reported as the same thing as a wrong answer.** "The order never settled"
  and "the order settled wrongly" have different causes and different fixes.
- **Starting stock cannot be assumed.** The check runs repeatedly against a database that keeps what
  earlier runs left. Every quantity assertion must be relative to what was observed immediately
  before, never to a constant.
- **Two scenarios in one run must not contaminate each other.** The second must not inherit the
  first's leftovers, and running only the second must give the same result as running both.
- **Both outcomes cannot be produced by one payment configuration.** Exercising the refusal path
  requires the payment service to be running under a different setting than the success path.
- **The check itself can be the thing that is broken.** A run that exercises nothing and reports
  nothing is indistinguishable from a run that exercised everything and found nothing wrong, unless
  it says which.
- **An order belongs to whoever placed it.** The check must read the order's state as its owner,
  through the same path a customer would, rather than going around the ownership rule.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The checkout flow MUST be exercised automatically on every proposed change, without a
  person performing any step.
- **FR-002**: An order MUST be placed through the same public interface a customer uses, identified
  the same way a customer is identified. A path that bypasses either proves nothing about the path
  customers take.
- **FR-003**: The check MUST confirm the order reaches a completed state.
- **FR-004**: The check MUST confirm the available stock decreased by **exactly** the quantity
  ordered. Confirming only the order's state would have passed during the failure this feature
  exists to prevent.
- **FR-005**: The check MUST confirm no stock remains held once the order has completed.
- **FR-006**: The check MUST exercise the path where payment refuses, and confirm the order reaches
  a failed state.
- **FR-007**: The check MUST confirm that, after a refused payment, the available stock is exactly
  what it was before the order — held units returned, not stranded.
- **FR-008**: Stock quantities MUST be compared against a reading taken immediately before the
  order, never against a fixed expected value.
- **FR-009**: The check MUST distinguish, in what it reports, between an order that settled
  incorrectly and an order that never settled at all.
- **FR-010**: When the check cannot run, it MUST report that plainly, and the report MUST NOT be
  mistakable for a pass.
- **FR-011**: The check MUST state which scenarios it exercised, so that exercising none is visible.
- **FR-012**: The check MUST be demonstrated to fail against a deliberately broken flow, and that
  demonstration MUST be recorded rather than described as intended.
- **FR-013**: A failing check MUST prevent the change from being accepted, in the same way the
  existing build and authentication checks do.
- **FR-014**: The check MUST NOT require production behaviour to be altered in order to be testable.
  A seam added purely so a test can steer the system changes what is being verified.

### Key Entities

- **A checkout attempt**: One order placed by one customer for one item and quantity, from placement
  to a final state. Either it completes or it fails; there is no third outcome this check accepts.
- **A stock reading**: The quantity available for an item at a moment in time, taken from the service
  that owns it. Meaningful only as a before-and-after pair.
- **A settled state**: The point after which an order's outcome no longer changes. Everything the
  check asserts is asserted after this point, never before.
- **A negative control**: A deliberately broken flow used to prove the check can fail. Without one,
  a passing check and an absent check are indistinguishable.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A change that breaks the cross-service checkout flow is rejected before acceptance,
  with **no person having to run anything**.
- **SC-002**: The specific failure that motivated this feature — an order completing while its stock
  stays held — is detected. Reproducing it makes the check fail.
- **SC-003**: Both outcomes are exercised on every run: one completed order and one failed order.
  Exercising one of the two is a partial result and is reported as such.
- **SC-004**: After a completed order, the available quantity differs from the reading taken before
  it by exactly the amount ordered — **0 tolerance**.
- **SC-005**: After a failed order, the available quantity is identical to the reading taken before
  it — **0 units stranded**.
- **SC-006**: A deliberately broken flow makes the check fail, demonstrated in **both** directions:
  broken fails, intact passes.
- **SC-007**: A run that could not execute is distinguishable from a passing run by reading the
  result alone, without opening logs.
- **SC-008**: The pipeline's total duration stays within a range people will keep waiting for; the
  before and after numbers are recorded rather than assumed.

## Assumptions

- **Payment's outcome is fixed when the service starts.** It is read once from configuration, so one
  running payment service produces one outcome. Exercising both paths therefore means running the
  scenarios against differently-configured instances. Rejected: making the outcome selectable per
  order, which would put test-steering logic into production code and weaken FR-014 — the check
  would then verify a path that exists only for the check.
- **The pipeline already starts databases, a broker, and three of the services**, for the existing
  authentication check. This feature extends that arrangement rather than inventing a new one. The
  three services not yet started there are what is missing.
- **The stub payment service stays a stub.** This feature verifies the saga, not a payment provider.
  Replacing the stub is a separate and much larger piece of work.
- **The check runs against a database that persists between runs**, like the existing checks, so it
  creates the data it needs rather than expecting a fixed fixture.
- **"Settled" is observable from outside.** The check reads the order's state through the
  customer-facing interface rather than inspecting any service's database, which would violate the
  rule that a service's data is its own.
- **Both failure routes already end the same way.** Reservation failure and payment refusal both
  produce a failed order today; the check asserts the outcome, not the route taken to it.

## Dependencies

- The checkout flow works end to end today, in both directions, and the pipeline already builds and
  starts services with databases and a broker. This feature observes an existing capability; it does
  not add one.
- Identity issues the tokens the check needs; obtaining one as a customer already exists and is
  already exercised by the authentication check.

## Out of scope

- **Changing any service's behaviour.** If the check fails, the defect is in the service, not in the
  check's right to notice it.
- **Performance or load testing.** This verifies correctness of one order at a time. Concurrency is
  already covered per-service, against a real database.
- **Replacing the stub payment gateway.**
- **Verifying the flow through the gateway as well as directly.** One route, established first.
- **Observability tooling.** Making a failure easier to diagnose is the next piece of work and is
  independent of making it detectable.
