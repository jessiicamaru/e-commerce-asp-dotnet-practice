# Contracts: Staff roles only in a back-office session

## HTTP (Identity, through the gateway)

- `POST /api/auth/login`, `/login/two-factor`, `/register`, `/register-seller`: unchanged bodies. The session's app comes from the `Origin` header.
- Every auth response (`AuthResponse`) gains `staffAccount: boolean`. `roles` lists what **this session** carries: no `Admin`/`Moderator` on the storefront.

## Configuration (Identity)

`BackOffice:Origins` - comma-separated origins, e.g. `http://portal.localhost:5174,http://portal.localhost:8089`
(`BackOffice__Origins` in the environment). An origin is compared by scheme, host and port, case-insensitively, with
no trailing slash.

No message or gRPC change.
