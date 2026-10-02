# Data Model: Orders are recognisable

No migration. `OrderSummaryResponse`, `SaleSummaryResponse` and `StaffOrderSummaryResponse` gain `lines: { productId, variantId, productName }[]` (at most three).
