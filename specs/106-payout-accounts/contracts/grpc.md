# gRPC contract: `PayoutAccounts` (Identity, served on its gRPC port)

```proto
service PayoutAccounts {
  rpc GetPayoutAccount (GetPayoutAccountRequest) returns (PayoutAccountReply);
}

message GetPayoutAccountRequest { string seller_id = 1; }

message PayoutAccountReply {
  bool found = 1;
  string bank_name = 2;
  string account_holder = 3;
  string account_last4 = 4;   // never the full number
  string updated_at = 5;      // ISO 8601 UTC
}
```

- **Caller**: Order's `RecordPayoutCommandHandler`, forwarding the administrator's `Authorization` header, the way
  `AddressReading` forwards a customer's.
- **Authorization**: `[Authorize(Roles = "Admin")]` on the service. Any other token gets `PermissionDenied`, which
  SC-005 mutates.
- **Why a seller id here**, when `AddressReading` has none: that call asks for the caller's own data. This one asks, as
  an administrator, about a seller, and the role is the permission.
