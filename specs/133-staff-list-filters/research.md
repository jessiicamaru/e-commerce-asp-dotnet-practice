# Research: Long staff lists can be searched and filtered

## D1 - Filters on the server where the list is paged

**Decision**: Vouchers and people are filtered in SQL; categories on the client.

**Rationale**: A paged list filtered in the browser filters one page. Categories come back whole already.

**Alternatives rejected**: Client filtering of the first page - silently incomplete.

## D2 - Words for a failure

**Decision**: Find an order reuses the customer's wording of a failure (`describeOrderStatus`).

**Rationale**: The same reason, the same words, and the reason codes are already mapped there.

**Alternatives rejected**: Showing the raw text - an internal message with a product id.
