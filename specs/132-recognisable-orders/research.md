# Research: Orders are recognisable

## D1 - Names from the order, pictures from the catalogue

**Decision**: The server sends up to three line previews as frozen; the row reads the first product's picture through the cached product lookup.

**Rationale**: The names must be the purchase's own (frozen, in the order's language). A picture is not frozen anywhere and the cart already reads pictures this way; a deleted product falls back to the tile.

**Alternatives rejected**: Freezing an image key on the order line - a migration for a thumbnail, and a dangling key when the image changes.

## D2 - Eight characters

**Decision**: `id.slice(0, 8)`, upper-case not applied.

**Rationale**: The notices already name orders so (specs/042); one reference everywhere is the point.

**Alternatives rejected**: A separate human order number - a sequence across instances, a migration, and a third name for the same order.
