# Research: A separate token audience for the back office

## D1 - Remove the staff roles, do not refuse the token

**Decision**: A token for the storefront's audience that carries a staff role is accepted, with the staff roles
removed from the principal. It is not refused.

**Rationale**: The token is valid for what the storefront does. Refusing it outright would turn a defect in issuing
into a signed-out shopper. Removing the roles keeps the guarantee that matters, "never staff outside the back office",
and nothing else changes.

**Alternatives rejected**: Failing the token (a 401 for a customer's ordinary request), or a policy on every staff
endpoint (dozens of attributes in nine services, and the next endpoint would forget it).

## D2 - In `OnTokenValidated`, beside revocation

**Decision**: The filter runs in the JWT bearer's `OnTokenValidated`, after the revocation check, in
`AddJwtAuthentication`.

**Rationale**: It is the one place every service validates a token. A new service gets the rule by calling the method
it must call anyway (CLAUDE.md: a new service's `AddJwtAuthentication`).

## D3 - One key, two audiences

**Decision**: Both audiences are signed with the same key. `BackOfficeAudience` has a default (`EcommerceBackOffice`),
so no service's configuration has to change.

**Rationale**: The audience says whom a token is for; the key says who issued it. Identity issues both. A string
default is safe from the configuration binder's array-append trap.
