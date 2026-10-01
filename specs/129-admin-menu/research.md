# Research: The admin console's menu is grouped and shows what is waiting

## D1 - Counts from the lists, under their own keys

**Decision**: A page of one from each queue's existing list, keyed `['staff-waiting', ...]`.

**Rationale**: Each list already says its `totalCount`; a count endpoint per queue would be five server changes for five numbers. The list hooks' keys carry the page but not its size, so reusing them would put the badge's one-row page on the list.

**Alternatives rejected**: One summary endpoint - it would need to ask Order and Catalog across services; the client already talks to both.

## D2 - Where an order came from

**Decision**: `state.from` on the link; the layout and the breadcrumb read it.

**Rationale**: An order belongs to whichever list was being worked; the address alone cannot say which.

**Alternatives rejected**: A query parameter - it would show in every shared order link.
