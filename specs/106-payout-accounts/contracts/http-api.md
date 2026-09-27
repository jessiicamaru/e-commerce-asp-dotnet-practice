# HTTP contract: Sellers say where their payouts go

Every route goes through the gateway's existing `/api/sellers/**` and `/api/orders/**` routes.

## `GET /api/sellers/me/payout-account`: Seller (Identity)

```json
{ "bankName": "Vietcombank", "accountHolder": "NGUYEN VAN A", "accountNumberMasked": "•••• 4321", "updatedAt": "2026-09-27T10:00:00Z" }
```

- `404` `No payout account yet.` when the seller has not saved one.
- `404` `This account does not sell on the shop.` for someone who is not a seller.

## `PUT /api/sellers/me/payout-account`: Seller (Identity)

```json
{ "bankName": "Vietcombank", "accountHolder": "NGUYEN VAN A", "accountNumber": "0071 0012 34321" }
```

- `200`: the masked read.
- `400` when the bank or holder is empty or over 100 characters, or when the number is not 6-34 letters or digits
  (spaces are removed first).

## `GET /api/sellers/payout-accounts?sellerIds=a,b`: Admin (Identity)

```json
[ { "sellerId": "…", "bankName": "…", "accountHolder": "…", "accountNumber": "0071001234321", "updatedAt": "…" } ]
```

A seller without an account is simply absent from the list. `403` for a moderator.

## `POST /api/orders/payouts`: Admin (Order), changed

- `409` `This seller has given no payout account; there is nowhere to pay.` when the seller has none. Nothing is
  claimed.
- `503` when Identity cannot be reached.
- Payout responses, for the seller's list and staff's, gain `paidToBank`, `paidToHolder` and `paidToAccountLast4`.
