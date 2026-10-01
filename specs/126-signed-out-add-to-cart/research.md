# Research: A signed-out shopper is offered Add to cart

## D1 - Keep D10: no preselected variant

**Decision**: With several sellable variants nothing is chosen until the shopper chooses; the issue's suggestion is declined.

**Rationale**: specs/020 decided it for a reason that still holds: a body and a kit differ by millions of dong, and a default is a choice made for the shopper. What the issue saw - a SKU shown while nothing was chosen - was the real defect, and is fixed.

**Alternatives rejected**: Preselecting the first in stock - fewer clicks, and the wrong kit in some carts.

## D2 - Carry the choice through sign-in in the address

**Decision**: `?variant=<id>` on the product address in `state.from`.

**Rationale**: The sign-in page already returns to `state.from`; an address survives the round trip, a component's state does not.

**Alternatives rejected**: Adding to a browser cart before sign-in and merging - a second cart model for one press.
