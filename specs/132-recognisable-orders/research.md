# Research: Orders are recognisable

## D1 - The biggest lines first

**Decision**: Up to three lines by line total, then name.

**Rationale**: The order bought is not recorded: line ids made in one millisecond do not keep it (found by the first test). The biggest line is what an order is remembered by - the camera, not its strap.

**Alternatives rejected**: Ordering by id - looked like the order bought and was not.

## D2 - Names from the order, pictures from the catalogue

**Decision**: The server sends up to three line previews as frozen; the row reads the first product's picture through the cached product lookup.

**Rationale**: The names must be the purchase's own (frozen, in the order's language). A picture is not frozen anywhere and the cart already reads pictures this way; a deleted product falls back to the tile.

**Alternatives rejected**: Freezing an image key on the order line - a migration for a thumbnail, and a dangling key when the image changes.

## D3 - Eight characters

**Decision**: `id.slice(0, 8)`, upper-case not applied.

**Rationale**: The notices already name orders so (specs/042); one reference everywhere is the point.

**Alternatives rejected**: A separate human order number - a sequence across instances, a migration, and a third name for the same order.
