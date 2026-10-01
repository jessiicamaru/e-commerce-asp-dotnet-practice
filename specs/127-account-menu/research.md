# Research: One account menu everywhere

## D1 - A list, not a shared component

**Decision**: One array of destinations; each place keeps its own rendering (dropdown items, sheet links, a grid).

**Rationale**: The three places draw differently and should; what drifted was the list.

**Alternatives rejected**: One `AccountNav` component for all three - a dropdown, a sheet and a page grid are different controls.
