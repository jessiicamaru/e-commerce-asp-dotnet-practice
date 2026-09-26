# Contracts: Vouchers (part 2 - the screens)

> Written on 2026-09-27, after the feature merged (#154), from the code at that merge, the pull request and
> docs/features/vouchers.md.

**This feature changed no external interface** - no endpoint, message, table or gateway route. It relies on the HTTP
contract of specs/069, [../069-vouchers/contracts/http-api.md](../../069-vouchers/contracts/http-api.md), as merged in
#153.

## What the storefront calls

All through axios (`baseURL: '/api'`), so through the Vite proxy or nginx to the gateway on :5000.

| Client call | Request | Used by |
| :-- | :-- | :-- |
| `Voucher.create(voucher)` | `POST /api/vouchers` with a `NewVoucher` - no owner | `voucher-page` create dialog |
| `Voucher.mine(page, pageSize)` | `GET /api/vouchers/mine?page=&pageSize=` | `voucher-page` list |
| `Voucher.disable(id)` | `POST /api/vouchers/{id}/disable` | `voucher-page`, after confirmation |
| `Order.quote({ addressId, shippingOption, voucherCodes })` | `GET /api/orders/quote?addressId=&shippingOption=&voucherCodes=A&voucherCodes=B` - repeated, the way ASP.NET binds a list | the summary's quote, and `useTryVoucher` with one more code |
| `Order.place({ …, voucherCodes })` | `POST /api/orders` `{ addressId, shippingOption, voucherCodes }` | `usePlaceOrder` |
| `Product.list` / the seller's own products | existing product reads | `product-picker` (administrator / seller) |

Responses read: `vouchers[]` and `items[].discount` on the quote and on `GET /api/orders/{id}`; `VoucherSummary` and
`VoucherPage`. A 409 or 400 from the server is shown in its own words - beside the voucher box at checkout, inside the
form on the voucher page.
