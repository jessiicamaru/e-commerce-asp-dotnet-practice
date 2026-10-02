# Research: Staff counts never share a list's cache

## D1 - Both: one count cache, and sizes in the keys

**Decision**: The pages use `useStaffWaiting`, and the list keys gain the page size.

**Rationale**: Moving the pages fixes these two readers; the size in the key makes the next page-of-one reader harmless too. Either alone leaves one of the two open.

**Alternatives rejected**: Only the keys - the counts would still be read three times under three names.
