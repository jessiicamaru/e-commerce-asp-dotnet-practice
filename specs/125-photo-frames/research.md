# Research: Product photographs fill their frame

## D1 - The photograph's own shape, not a better fixed one

**Decision**: Large images take their natural ratio (`h-auto`), capped by `max-h`; 4:3 only while loading and for the missing tile.

**Rationale**: Any fixed frame is right for one shape and bands every other; the photographs are the sellers', in whatever shape they took them. `object-cover` would remove the bands by cutting the product.

**Alternatives rejected**: 4:3 with `object-contain` - smaller bands, still bands; `object-cover` - crops the thing being sold.
