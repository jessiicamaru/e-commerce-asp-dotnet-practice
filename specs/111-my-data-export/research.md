# Research: A person downloads the data the shop holds about them

## D1 - Each service exports its own data, and the storefront composes the file

**Decision**:
- Every service that holds personal data answers `GET .../my-data` from its own tables, for the caller in the token.
- The storefront fetches the six answers and writes one file.

**Rationale**:

- Constitution I: each service owns its data, and none reads another's. This is the shape of `/admin/overview`
  (specs/047), which the client composes from three services.
- No new synchronous edge between services, and no message round trip, for a download a person asks for now and then.

**Alternatives rejected**:

- *Identity gathers everything over gRPC.* It would add five new synchronous edges into Identity for one button.
- *An export job: publish `ExportRequested`, collect `ExportPart`s, email a link.* That needs file storage, a job table
  and a delivery channel, for data small enough to answer inline.

## D2 - The inventory is declared in code and checked against the model

**Decision**:
- `Ecommerce.Shared.PersonalData` holds `PersonalDataInventory`: exported tables, withheld tables with a reason, and
  tables that are not personal.
- Each service declares its own inventory next to its export handler.
- A test in each service compares the declaration with `DbContext.Model`'s table names. Another test checks that every
  exported table is a key of the export's sections.

**Rationale**:

- The acceptance criterion "the export contains every table the person appears in" needs something that fails when a
  new table is not considered. This is the same shape as `EndpointAccessTests` (specs/089) and the notification kinds
  file (specs/048).
- Declaring a table withheld *with a reason the person reads* makes the export honest: the file says what exists and
  why it is not handed out.

**Alternative rejected**: a hand-written list in the docs. It is right on the day it is written and wrong at the next
migration.

## D3 - What is withheld, and why

| Table | Why |
| :-- | :-- |
| `users.PasswordHash`, `TwoFactorSecret` (columns, not exported) | secrets that sign a person in |
| `refresh_tokens` | the sessions' secrets; a person manages sessions by signing out |
| `password_reset_tokens`, `email_confirmation_tokens`, `two_factor_challenges`, `two_factor_recovery_codes` | hashes of one-time secrets |
| `sign_in_throttles` | a security counter keyed on the email, reset within minutes |
| Catalog `product_viewers` | a hash of a token id or a visitor id, which cannot be linked back to the person |
| Order `order_shipments` of a seller's sales, `order_items` of other buyers | other people's purchases (sellers read them at `/shop/sales`) |
| Cart `checkout_outcomes` | working records of a checkout; the order itself is in Order's export |

**Rationale**: Each of these is either a secret whose copy would weaken the account, data about other people, or data
that cannot be tied to the person. Saying so in the file is better than a silent omission.

## D4 - Audit entries: own actions, and decisions about the person, without staff identities

**Decision**: Activity exports the entries whose `ActorId` is the person (their own actions) and those whose
`AboutUserId` is the person (decisions about them). It exports action, category, summary, service and time. It never
exports the actor's email or role on entries about the person, or the before/after snapshots.

**Rationale**:
- The person has a right to the decisions made about them.
- The staff member who made a decision has a privacy interest too.
- Snapshots can contain other people's fields.

## D5 - One service down: a marked, partial file

**Decision**: the storefront writes whatever answered, puts `{ "unavailable": true }` in place of each missing service,
and names the missing services on the page.

**Rationale**: A person asking for their data should get what can be given now. A file that silently lacks a section
would read as complete.
