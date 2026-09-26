# Research: Behaviour promised but not held by a test

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #186

---

## D1 - Row 7: fix the paging, with the rule the rest of Catalog uses

**Decision**: `GetReviewsForStaffQueryValidator` - `PageNumber > 0`, `PageSize` 1-50 - beside the existing
`PagingValidators` for the public review list.

**Rationale**: `GetProductReviewsQuery` and `GetReviewQueueQuery` are both held to 1-50; the staff list was the one
paged query in the file with no validator. A test can only assert the corrected behaviour.

**Alternatives considered**: add the query to `PagingValidators` as a second interface - rejected: a class validating
two request types is harder to find from either.

---

## D2 - Row 3: a restart, built by the fixture

**Decision**: `OrderTestFixture.RestartedWith(Action<IServiceCollection>)` builds the service's provider again, against
the same database, with extra registrations last (so they win). The test registers a fixed `ITaxRates` of 25%, reads the
order placed at 10%, and then checks out once more to show the new rate is in force.

**Rationale**: `ConfiguredTaxRates` reads configuration in its constructor, and it is a singleton - changing
configuration mid-test changes nothing, which would make the test pass for the wrong reason. The final checkout is what
proves the restart took effect.

**Alternatives considered**: mutate the in-memory configuration - rejected for the reason above.

---

## D3 - Row 2: two guards, and a test that needs both broken

**Decision**: A hook test that fails the fetch, waits for the query to settle in error (reading its state from the
`QueryClient`), and asserts the notice still reads its bundled words; plus a control that an edit is applied.

**Finding**: Two mutations were tried before one was caught, and the reason is worth keeping:

- applying `data ?? {}` on failure is inert - the effect depends on `data`, which is `undefined` before and after the
  failure, so it never runs again;
- making it run is still harmless - `applyWording` builds each language from the bundle and deep-merges, so an empty
  set changes nothing.

Only a change that both applies on failure **and** stops starting from the bundle blanks the notices, and that is the
recorded mutation. The test holds the outcome (words stay), not one mechanism.

**Alternatives considered**: assert that `applyWording` is not called on failure - rejected: it tests the mechanism, and
calling it with `{}` would be correct too.

---

## D4 - Row 4 includes the variant

**Decision**: The saga test asserts the variant in `ReserveInventoryCommand` as well as the currency in
`ProcessPaymentCommand`.

**Rationale**: CLAUDE.md's relay gotcha - an orchestrator built against an older `OrderItemDto` dropped `VariantId` and
the wrong variant's stock moved - had no test; it is the same "relayed, never re-derived" property as the currency.

---

## D5 - Rows 5 and 6: concurrency through the real handlers

**Decision**: Five simultaneous `Send`s, each in its own scope and DbContext, counting successes against `ConflictException`.

**Rationale**: The guarantees are the database's - a guarded `UPDATE` (row 5) and a partial unique index plus the handler
mapping its violation to 409 (row 6). Real PostgreSQL, real handlers; a 500 from an unmapped violation escapes the count
and fails the test, which is exactly row 6's mutation.

---

## D6 - Row 1: the pattern of the existing save test

**Decision**: For each of reset and restore in both editors, hold the service call's promise, click, unmount, resolve,
and expect the toast.

**Rationale**: It is the specs/080 finding - a callback handed to `mutate()` does not run for a component that is gone -
and the save test already has this shape. The mutation for each is the `mutate(..., { onSuccess })` form.
