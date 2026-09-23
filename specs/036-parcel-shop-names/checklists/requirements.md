# Specification Quality Checklist: Which shop each parcel comes from

- [X] No implementation details in the spec
- [X] Requirements testable; success criteria measurable
- [X] Edge cases: older orders, a name Catalog has not heard yet, an older Catalog
- [X] Scope bounded: no contact, no profile, no backfill
- [X] No [NEEDS CLARIFICATION] markers

The only decision taken in auto mode: the name is **frozen** at checkout rather than looked up at
read time (research D1) - the same reasoning as the seller id in specs/034.
