# Research: The catalogue on a phone

## D1 - Two columns, not a smaller minimum

**Decision**: `grid-cols-2` below `sm`, the `auto-fill` of 15rem from `sm`.

**Rationale**: `minmax(15rem,1fr)` cannot fit two in 358px; lowering the minimum for everyone would also squeeze desktop cards. Two fixed columns on a phone is what the shop front already does and what shops do.

**Alternatives rejected**: A list layout with a thumbnail - loses the photograph, which is what sells a camera.

## D2 - A compact hero rather than none

**Decision**: Keep the heading, the button and the count; hide the featured photograph below `sm`; chips in one scrolling row.

**Rationale**: The hero says what the shop is; the featured camera repeats one of the products right below it, and is ~500px of the 1,350px before the first product.

**Alternatives rejected**: Hiding the hero on phones - the first screen would start with a filter form.
