# Feature Specification: A person downloads the data the shop holds about them

**Feature Branch**: `111-my-data-export` | **Created**: 2026-10-01 | **Issue**: #217 (part 1 of 2; part 2, deleting an account, is specs/112)

**Status**: Draft

**Input**: Issue #217 - "a person cannot delete their account or download their data". This part is the download.

## Why

Vietnam's Decree 13/2023/ND-CP on personal data protection gives a data subject the right to know what personal data
is processed about them, and to have a copy. Today a person can see their orders, addresses and saved products page by
page, but nothing gives them everything the shop holds. Nothing even says which tables that is: their data is spread
over six services, each with its own database.

## User Scenarios & Testing *(mandatory)*

### US1 - Download my data (Priority: P1)

A signed-in person presses "Download my data" on their account page and receives one JSON file. The file holds
everything the shop keeps about them:
- their profile and addresses;
- their orders with the delivery copies frozen on them, their returns and payments;
- their reviews, questions, saved products and reports;
- their cart;
- their notifications;
- the audit entries of their own actions and of decisions about them.

A seller also gets their shop, payout account (masked), vouchers and payouts.

**Why this priority**: It is the right the issue names, and deleting an account (part 2) needs the same inventory of
where a person's data lives.

**Acceptance Scenarios**:

1. **Given** a customer with an address, an order, a review and a saved product, **When** they download their data,
   **Then** the file holds each of them.
2. **Given** another customer's rows in the same tables, **Then** none of them is in the file.
3. **Given** anything held about them that is not handed out - a password hash, session tokens, one-time-code hashes,
   the sign-in counter - **Then** the file names each such table and says why it is withheld, instead of silently
   leaving it out.
4. **Given** an anonymous caller, **Then** 401 from every export route.

---

### US2 - The inventory stays complete (Priority: P1)

A developer adds a table that holds a person's id. The build's tests fail until the table is declared as exported,
withheld with a reason, or not personal.

**Acceptance Scenarios**:

1. **Given** each service's EF model, **Then** every mapped table is declared in exactly one of: exported, withheld,
   not personal.
2. **Given** a table declared exported, **Then** it appears as a section of that service's export.

### Edge Cases

- **One service down**: the storefront still downloads what the others answered. It marks the missing part in the file
  and says so on the page, rather than handing over a file that looks complete.
- **Secrets never leave**: a password hash, the refresh tokens, reset/confirmation/challenge/recovery-code hashes, the
  TOTP secret, and a payout account's full number (masked, as on the seller's own page).
- **Other people's data**: an order's lines belong to the buyer's export. A seller's sales, which hold other people's
  purchases, are withheld from the seller's export and can be read at `/shop/sales`.
- **Audit entries about the person** carry the action, summary and time, never the acting staff member's identity or
  the snapshots.
- **Inventory** holds nobody's id (reservations are by order), so it has no export. **The orchestrator** keeps an order's
  saga state, including the buyer's id, while the order settles. It is withheld as working state and has no HTTP.
- **Size**: the export is not paged. A person's rows number in the hundreds, not millions.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Each service holding personal data answers `GET .../my-data` for the signed-in caller, from the token:
  - Identity: `/api/auth/me/data`
  - Catalog: `/api/products/my-data`
  - Order: `/api/orders/my-data`
  - Cart: `/api/cart/my-data`
  - Payment: `/api/payments/my-data`
  - Activity: `/api/notifications/my-data`

  Each returns `{ service, exportedAt, sections: { <table>: [...] }, withheld: [{ table, reason }] }`.
- **FR-002**: Each service declares its personal-data inventory in code: every table exported, withheld (with the reason
  the person reads) or not personal. A test compares the declaration with the EF model's tables, and another checks
  that every exported table is a section of the export.
- **FR-003**: No secret, hash, token, or other person's personal data is in any export.
- **FR-004**: The storefront's account page has "Download my data". It fetches all six exports, writes one file
  `my-data-<date>.json` (`{ exportedAt, person, services: {...} }`), and names any service that did not answer.

## Success Criteria *(mandatory)*

- **SC-001**: For each service, a test puts a row in every exported table for two people and shows that each person's
  export holds their own rows and none of the other's.
- **SC-002**: Adding a table to any of the six models without declaring it fails a test.
- **SC-003**: Bruno reads every export through the gateway, and an anonymous request to each is 401.

## Assumptions

- JSON is the format: machine-readable, which the decree's right to a copy is served by, and readable enough.
- Only the person themselves exports their data. Staff exporting on a person's behalf is out of scope.
- Deleting an account is specs/112, the second PR of #217.
