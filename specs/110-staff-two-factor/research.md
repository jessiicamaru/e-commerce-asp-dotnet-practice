# Research: Staff sign in with a second factor

Background on how TOTP works, with a worked example: [docs/features/auth/totp-two-factor.md](../../docs/features/auth/totp-two-factor.md).

## D1 - TOTP (RFC 6238), written here, not a package

**Decision**: TOTP with SHA-1, 6 digits and a 30-second step. It is about twenty lines over `HMACSHA1` in
`Ecommerce.Identity.Infrastructure/Security/Totp.cs`, and is tested against the RFC's Appendix B vectors.

**Rationale**:

- Every authenticator app supports exactly these parameters. SHA-256 and 8 digits are in the RFC but poorly supported
  by apps.
- The algorithm is small and fixed, and its test vectors are published, so writing it costs less than a dependency to
  audit.

**Alternatives rejected**:

- *SMS or email codes.* They can be intercepted (SIM swap, a compromised mailbox), cost money, and email is already the
  password-reset channel, so it would not be a second factor for it.
- *WebAuthn/passkeys.* They resist phishing, which TOTP does not, but they need a browser ceremony, credential storage
  and attestation choices. Recorded as the next step, not this one.
- *A NuGet TOTP package.* It adds a dependency for twenty lines.

## D2 - Staff roles only in a verified session, decided where tokens are signed

**Decision**:
- `refresh_tokens.TwoFactorVerified` records whether the session was established with a second factor.
- `JwtTokenGenerator` writes `Admin` and `Moderator` into the access token only for such a session.
- A staff member without 2FA gets a session holding their other roles, and `twoFactor: "SetupRequired"`.

**Rationale**:

- Every service already authorizes from the token's roles. Withholding the roles makes every staff endpoint, in all
  nine services, refuse an unverified session with **no change outside Identity** (Constitution I).
- The alternative, a claim that every service must check, is a rule nine services would each have to remember. The
  next staff endpoint would forget it.
- A staff member still needs a session to reach the setup page, so refusing the sign-in outright would lock them out.

**Alternatives rejected**:
- *An `amr` check in `AddJwtAuthentication`.* It would work, but it is a second place deciding what a role means.
- *Refusing staff sign-in until enrolled.* That needs a separate enrolment path with no session.

## D3 - A stored, hashed, single-purpose challenge between the two steps

**Decision**: the right password for an account with 2FA creates a row in `two_factor_challenges` holding:
- the SHA-256 of a random token;
- five minutes of life;
- a failed-attempt count.

The response carries only the token. The second step claims it with one guarded `UPDATE ... WHERE "UsedAt" IS NULL AND
"ExpiresAt" > now AND "FailedAttempts" < 5`.

**Rationale**:

- The challenge grants nothing by itself.
- A challenge that dies after five wrong codes, together with the email pause of specs/062, bounds guessing: at most a
  3-in-a-million chance per guess.
- Hashing it follows the reset token (specs/061): a database read never yields a usable challenge.

**Alternative rejected**: a signed JWT as the challenge. It is stateless, so it cannot count failures or be used only
once.

## D4 - The secret is encrypted with AES-GCM under `TWO_FACTOR_KEY`

**Decision**: store `nonce ‖ ciphertext ‖ tag`, base64. The key is 32 bytes, base64, from configuration. Identity
refuses to start without a usable one.

**Rationale**:

- The secret must be read back to compute codes, so it cannot be hashed.
- ASP.NET Data Protection would need its key ring persisted and shared across container restarts and instances. That
  is a new package and a new table, where one explicit key, like `JwtSettings:Secret`, is simpler to operate.
- AES-GCM authenticates the ciphertext, so a tampered row fails to decrypt instead of producing wrong codes.

**Alternative rejected**: Data Protection with `PersistKeysToDbContext`. It is sound, but carries more moving parts
for one column.

## D5 - Replay: the last used step, in the same statement that accepts the code

**Decision**: `users.TwoFactorLastStep` is updated with a guarded `UPDATE ... WHERE "TwoFactorLastStep" IS NULL OR
"TwoFactorLastStep" < @step`. Zero rows means the code's window was already used.

**Rationale**: Two sign-ins racing with the same code both compute it as valid. Only the guarded write can let exactly
one of them through.

## D6 - Recovery codes: ten, hashed, single-use

**Decision**:
- Ten codes of 10 base32 characters, shown as `XXXXX-XXXXX`, with only their SHA-256 stored.
- A code is spent by one guarded `UPDATE ... WHERE "UsedAt" IS NULL`.
- A new set replaces the old one and needs a current TOTP code.

**Rationale**: This is the standard escape from a lost phone, shaped like the other single-use tokens here.

## D7 - Wrong codes count toward the email pause

**Decision**: a wrong code calls `ISignInThrottle.RecordFailureAsync` for the account's email, like a wrong password.

**Rationale**: specs/064 made the same choice for the password change, for the same reason: every way of guessing
one secret must share one limit.

## D8 - Reset by another administrator only

**Decision**: `DELETE /api/users/{id}/two-factor` is Admin only and never for oneself. It revokes every session,
publishes `AccessTokensRevoked`, is audited under Security, and emails the owner (`TwoFactorReset`).

**Rationale**:

- The recovery path is the usual way around 2FA, through social engineering. Only an administrator can reset, and
  they cannot reset themselves.
- The owner is told at once, so an unwanted reset is noticed.
- A moderator may lock accounts, but taking away someone's second factor is a stronger act.

## D9 - Development and CI: `ADMIN_TOTP_SECRET`

**Decision**: when `ADMIN_TOTP_SECRET` (base32) is set, `DataInitializer` enrols the seeded administrator with it if
they have no 2FA. `verify-auth.sh`, `verify-saga.sh`, Bruno and Playwright compute the administrator's codes from it.
Production leaves it unset, and the administrator enrols through the page at their first sign-in.

**Rationale**:
- Mandatory staff 2FA must be exercised by the same scripts CI already runs, not switched off for them.
- A secret known to CI is harmless in a throwaway database.

**Alternative rejected**: `TwoFactor:RequiredForStaff=false` in CI. CI would then test a different system from the
one deployed.

## D10 - What the storefront may know

**Decision**: `AuthResponse` gains `twoFactor` (`null`, `"Required"` or `"SetupRequired"`) and `challenge`, and
`Roles` lists the token's effective roles.

**Rationale**: The storefront draws the second step and the setup redirect from these fields. The server decides
everything on its own, as for `Roles` since specs/028.
