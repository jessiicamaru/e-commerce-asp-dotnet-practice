# Research: Order lines fit any width

## D1 - A list of grid rows, not a table

**Decision**: Each line is a `grid-cols-[minmax(0,1fr)_auto]` row: details and "qty × price" in the first column, the total and its discount in the second.

**Rationale**: A table sizes columns from every cell's content and cannot drop to fewer columns on a narrow screen; the middle "qty × price" column is what squeezed the name and pushed the total out. Two columns, one of them allowed to shrink to nothing, cannot overflow: the total is as wide as its own text and the details take what is left.

**Alternatives rejected**: `overflow-x-auto` on the table - a total you have to scroll to is still hidden; container queries switching layouts - two layouts to keep right instead of one that fits everywhere.

## D2 - The total's rule

**Decision**: The total's `dt` and `dd` in one `div` with `col-span-2 grid-cols-subgrid` and the border.

**Rationale**: A border on two grid cells is interrupted by the column gap; one row spanning both columns is not. `dl > div > dt + dd` is valid HTML.

**Alternatives rejected**: An `<hr>` inside the `dl` - not allowed there.
