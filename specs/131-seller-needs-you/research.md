# Research: A seller's home says what needs them

## D1 - Count on the server, not in the window

**Decision**: A `status` filter on the sales query.

**Rationale**: The overview reads the latest 100 sales; a seller with a backlog older than that would see a count that is short. The filter is a `WHERE` on the part the query already joins.

**Alternatives rejected**: A count endpoint - a second query of the same rows; counting the window - wrong past 100.

## D2 - A panel, not a banner

**Decision**: "Needs you" lists each waiting kind with its count and a link.

**Rationale**: Several things can wait at once; each has a different place to act.

**Alternatives rejected**: Toasts on load - gone before they are read.
