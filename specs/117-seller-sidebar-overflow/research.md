# Research: The seller sidebar keeps to its column

## D1 - Why the sidebar grew

**Decision**: Give the `<aside>` and the shop card `grid-cols-[minmax(0,1fr)]`.

**Rationale**: Both are CSS grids with an implicit `auto` column. An `auto` track's minimum is its content's min-content, and a `truncate` (no-wrap) name's min-content is the whole name - so the column, and the card, became as wide as the name and overflowed the 15rem column the outer grid gave the aside. `min-w-0` on the inner div did not help: it lowers the flex item's automatic minimum, not the grid track's min-content.

**Alternatives rejected**: A maximum length on shop names - it fixes this page and not the next frame to show one; `overflow-hidden` on the aside - hides the symptom and would clip focus rings.

## D2 - How to prove it

**Decision**: An assertion in the Playwright flows, whose seller gets a long shop name.

**Rationale**: jsdom computes no layout, so a unit test could only assert a class name - which says nothing about the overlap. The flows already sign a seller in; comparing two bounding boxes is the actual property.

**Alternatives rejected**: A visual-diff screenshot test - brittle across fonts and data.
