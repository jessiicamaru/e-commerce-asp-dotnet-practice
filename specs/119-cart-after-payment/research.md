# Research: The cart empties on screen when the order is paid

## D1 - Re-read the cart on the settlement, and once more

**Decision**: Invalidate `queryKeys.cart()` when the watched order's status leaves settling, and again after 2 seconds.

**Rationale**: `usePlaceOrder` already re-reads the cart when the order is placed, but Cart empties it only on `OrderCompletedEvent` (by design: a declined card must leave the cart full). The order page polls the order every second, so its settlement is the moment the client knows the cart has changed. Order and Cart consume the same event independently, so Cart may apply it a moment after Order: one more read covers that without polling the cart.

**Alternatives rejected**: Polling the cart while the order settles - a request a second for something that changes once; removing the lines on the client - a second opinion on what the cart holds, wrong when the payment fails.
