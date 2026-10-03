# Quickstart: Pay with VNPay (sandbox)

## Tests

```bash
cd server && DB_PASSWORD=... dotnet test tests/Ecommerce.Payment.Tests
cd client && npm test
```

Expected: the VNPay tests pass:
- the signature against a fixed vector;
- the checkout opened and not decided;
- each IPN code;
- a concurrent IPN recorded once;
- ownership.

The existing Payment tests pass unchanged.

## The saga, through the simulator

```bash
cd server
PAYMENT_PROVIDER=VnPay dotnet run --project src/Services/Payment/Ecommerce.Payment.WebApi/   # with the other five
dotnet run --project src/Tools/Ecommerce.VnPaySimulator/
ADMIN_EMAIL=... ADMIN_PASSWORD=... ADMIN_TOTP_SECRET=... SAGA_E2E_SCENARIO=vnpay ../.github/scripts/verify-saga.sh
```

Expected: the paid order settles to Paid with the stock deducted. The cancelled one fails with the stock released and
nothing held.

## In a browser (compose)

```bash
cd server
PAYMENT_PROVIDER=VnPay docker compose -f docker-compose.yml -f docker-compose.app.yml up -d
```

1. Place an order in dong.
2. On the order page click **Pay with VNPay**. The simulator at `http://localhost:5064` shows the amount.
3. Click **Pay**. You land on the order page, which turns Paid.

## Against VNPay's sandbox (the merchant's step)

1. Register a sandbox merchant at sandbox.vnpayment.vn. It gives a `TmnCode` and a `HashSecret`.
2. Set `VNPAY_TMN_CODE`, `VNPAY_HASH_SECRET` and `VNPAY_PAY_URL=https://sandbox.vnpayment.vn/paymentv2/vpcpay.html`.
3. Register the IPN address `https://<shop>/api/payments/vnpay/ipn` in the merchant portal.
4. Pay with VNPay's published test card.
