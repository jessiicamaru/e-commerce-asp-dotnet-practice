# Specification Quality Checklist: The Shop Decides What Things Cost

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-21
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

**"No implementation details" needed a second pass, and gRPC is the reason.** The requirement that
started this feature names gRPC, h2c and a second port. None of that is in the spec, because none of
it is what the feature is *for* — a customer paying the shop's price is the outcome, and it would be
the same outcome over REST. The mechanism is a real decision with real consequences and it belongs
in the plan's research, where the rejected alternatives can sit beside it. The one place the spec
does touch it is the Assumptions entry about a synchronous call, because *"the shop being
unreachable now stops orders"* is a consequence a stakeholder has to agree to, not a technical
detail.

**Three user stories are all P1, which is unusual and deliberate.** US1 is the hole. US2 is the half
that would look correct on the day and rewrite history later — fetching the right price and storing
a *reference* to it passes every test US1 can write. US3 is the only one about time: this defect
survived because sixty-one tests were shaped so none of them could see it, and fixing the code
without fixing that blindness leaves it free to return.

**FR-003 says the field must be absent rather than ignored.** That is stronger than it needs to be
for correctness and it is the same call the project already made for the caller's identity. A field
the server ignores is an invitation for somebody to start honouring it.

**Not asked as a clarification**: how long to wait for the shop, and how many times to retry. Both
have reasonable defaults and are tuning decisions for the plan. FR-010 and FR-011 require a bound
and require exceeding it to be reported as itself, which is the part that matters.

**What the spec deliberately does not settle**, and the plan must: what happens to an order whose
*second* line refers to a missing product after the first was validated. The edge case says
part-accepting is worse than refusing; it does not say whether the shop is asked once per order or
once per line, and that choice decides how cleanly "refuse in full" can be implemented.
