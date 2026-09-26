# Specification Quality Checklist: Parcel returns

> Completed on 2026-09-27, after the feature merged (#149), from the code at that merge, the pull request and
> docs/features/returns.md.

**Created**: 2026-09-25 | **Feature**: [spec.md](../spec.md)

- [x] No [NEEDS CLARIFICATION] - whole parcels, window, who pays, hold not debt, dispute are recorded decisions
- [x] Every transition has acceptance, including concurrency and lateness
- [x] Scope bounded: the UI is part 2; partial returns and photos are out

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - *explained*: the stories name the routes and the
  `Returns:WindowDays` setting, as the specs of this repository do, so the acceptance can be run as written. The
  requirements (FR-001 to FR-016) are stated as behaviour.
- [x] Focused on user value and business needs - the buyer's refund, the seller's goods and money
- [x] Written for non-technical stakeholders - *explained*: the money rules (US4) are written for the reader who pays
  sellers; the transition details are for the implementer
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous - each maps to a named test in `ReturnTests`, `ReturnRefundTests` or
  `RestockReturnTests`
- [x] Success criteria are measurable - once-only, claim equals balance, 409 on every late step
- [x] Success criteria are technology-agnostic - *explained*: SC-001 to SC-005 cite the tests that measured them, which
  is how this repository records evidence
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified - concurrency, the window's edge, lapses, deleted variants, two parcels of one order
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified - delivery (specs/040), fulfilment roles (specs/039), the Payment stub

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows - request, decide, dispute, send back, receive, money
- [x] Feature meets measurable outcomes defined in Success Criteria - see the PR's evidence, quoted in the spec
- [x] No implementation details leak into specification - *explained* as under Content Quality

## Notes

The one decision that changed what the issue asked for - a hold instead of a debt (US4) - is recorded in the spec, in
research D2 and as decision 50. Who approved it is not recorded beyond the pull request.
