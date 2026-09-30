# Quickstart: Staff sign in with a second factor

## Setup

`server/.env` needs two new values:

```bash
TWO_FACTOR_KEY=<32 random bytes, base64>     # python -c "import os,base64;print(base64.b64encode(os.urandom(32)).decode())"
ADMIN_TOTP_SECRET=<base32, development only>  # python -c "import os,base64;print(base64.b32encode(os.urandom(20)).decode())"
```

To compute the administrator's current code:

```bash
python -c "import hmac,hashlib,struct,time,base64,os;s=base64.b32decode(os.environ['ADMIN_TOTP_SECRET']);h=hmac.new(s,struct.pack('>Q',int(time.time())//30),hashlib.sha1).digest();o=h[-1]&15;print(str((struct.unpack('>I',h[o:o+4])[0]&0x7fffffff)%10**6).zfill(6))"
```

## Scenario 1 - Tests

```bash
cd server && DB_PASSWORD=... dotnet test tests/Ecommerce.Identity.Tests --filter "TwoFactor|Totp"
cd ../client && npx vitest run src/pages/sign-in src/pages/account-two-factor
```

## Scenario 2 - Through the gateway (Bruno)

1. `login admin` answers `twoFactor: "Required"` with no token. `login admin: the code` exchanges the challenge for a
   token that carries `Admin`.
2. The same code again is 400 (replay).
3. A moderator signs in: `SetupRequired`, and Catalog's review queue answers 403. The moderator sets up and confirms
   with a computed code, renews the session, and the review queue answers 200.

## Scenario 3 - CI scripts

`verify-auth.sh` and `verify-saga.sh` sign the administrator in with a code computed from `ADMIN_TOTP_SECRET`.
`verify-auth.sh` also checks that the first step alone yields no token.

## Scenario 4 - Mutations (each must turn a test red)

| Mutation | Caught by |
| :-- | :-- |
| Staff roles issued without verification | the unverified token carries `Admin` |
| The replay guard dropped | the same code accepted twice |
| The window widened to ±2, or narrowed to 0 | the window tests |
| The challenge not single-use | the second exchange succeeds |
| The failure count not checked | a sixth code accepted |
| Wrong codes not counted toward the pause | the pause test |
| Recovery code reusable | the second use succeeds |
| Staff allowed to turn it off | 403 test |
| Reset of oneself allowed | 403 test |
| Rotation dropping `TwoFactorVerified` | the renewed token loses `Admin` |
