# Contracts: A seller's home says what needs them

`GET /api/orders/sales?page=&pageSize=&status=` - `status` optional: `Paid` (part waiting, not cancelled), `Preparing`, `Shipped`, `Cancelled` (the part or the order). Anything else is 400 on `Status`. The response shape is unchanged.
