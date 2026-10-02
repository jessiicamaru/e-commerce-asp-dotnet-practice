# Feature Specification: A separate token audience for the back office

**Feature Branch**: `feat/280-back-office-audience` | **Created**: 2026-10-03 | **Issue**: #280

**Status**: Draft

**Input**: Issue #280, "a separate token audience for the back office" - the stricter second line
[ADR-003](../../docs/architecture/adr-003-storefront-and-back-office.md) recorded after specs/138.

## Why

specs/138 keeps staff roles out of storefront sessions **in Identity**. It is one decision in one place, and that is
its strength and its risk: a defect there, or a token signed by anything else holding the key, would be honoured as
staff by every service. A second line belongs where the tokens are **used**. Each service should refuse a staff action
from a token that was not issued for the back office, whatever roles it carries.

## User Scenarios & Testing *(mandatory)*

### US1 - A token not issued for the back office never acts as staff (Priority: P1)

**Acceptance Scenarios**:

1. **Given** a token for the storefront's audience that carries `Admin`, **When** it calls a staff endpoint in any service, **Then** it is 403. The roles are removed once the token is validated, so the token can still do everything else it was for.
2. **Given** a token for the back office's audience carrying `Admin`, **Then** staff endpoints accept it.
3. **Given** a token for neither audience, **Then** it is 401, as before.

---

### US2 - Identity issues each app's tokens for that app (Priority: P1)

**Acceptance Scenarios**:

1. **Given** a back-office session (specs/138), **Then** its access tokens are issued for `BackOfficeAudience`. **Given** a storefront session, **Then** they are issued for the existing audience.
2. **Given** a forwarded token (Order to Cart or Identity, Identity to Order, over gRPC), **Then** it keeps working, because every service accepts both audiences.

### Edge Cases

- A storefront customer's token is unchanged: same audience, same claims.
- Seller, Customer and every other role are never removed. Only the staff roles are tied to the back office.
- The check sits beside the revocation check (specs/065), in the one place every service validates a token, so a new service gets it by calling `AddJwtAuthentication`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `JwtSettings.BackOfficeAudience` (default `EcommerceBackOffice`). Every service accepts tokens for `Audience` or `BackOfficeAudience`.
- **FR-002**: After a token is validated, `AddJwtAuthentication` removes `Admin`/`Moderator` from the principal unless the token's audience is `BackOfficeAudience`.
- **FR-003**: Identity issues a back-office session's access tokens for `BackOfficeAudience`.
- **FR-004**: Docs: the auth pages, the back office page, ADR-003 progress, CLAUDE.md.

## Success Criteria *(mandatory)*

- **SC-001**: Through a real ASP.NET Core pipeline, a staff endpoint answers 403 to a storefront-audience token with `Admin` and 200 to a back-office one (tests).
- **SC-002**: Bruno, the verify scripts and Playwright pass unchanged: they act as staff through back-office sessions.

## Assumptions

- One signing key for both audiences. Separate keys would need key distribution per service, for no gain while every service already trusts Identity's key.
