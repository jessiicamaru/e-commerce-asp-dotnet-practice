# Feature Specification: Staff see a deleted account as deleted

**Feature Branch**: `fix/241-deleted-accounts` | **Created**: 2026-10-01 | **Issue**: #241

**Status**: Draft

**Input**: Issue #241 - "deleted accounts are listed as Active with actions" - found in the screen review of 2026-10-01.

## Why

Since specs/112 a person can delete their account: the row stays, emptied, so the books keep their references. The staff list showed those rows - `deleted-…@deleted.invalid`, no name, no roles - as **Active**, with the menu to lock, ban or grant Moderator. A moderator could lock somebody who no longer exists, and every page of the list filled with blanks as people left.

## User Scenarios & Testing *(mandatory)*

### US1 - A deleted account says so and cannot be acted on (Priority: P1)



**Acceptance Scenarios**:

1. **Given** a deleted account shown to staff, **Then** it reads "Deleted" with the date, and offers no action.
2. **Given** staff send a lock, ban, unlock, lift, grant or revoke for a deleted account anyway, **Then** the answer is 409 `AccountDeleted` and nothing is written.

---

### US2 - The list is of people who still have an account (Priority: P2)



**Acceptance Scenarios**:

1. **Given** the users list, **Then** deleted accounts are left out unless staff tick "Show deleted accounts".
2. **Given** the box ticked, **Then** they are listed with their deletion date, newest first like the rest.

### Edge Cases

- A search by the old email finds nothing: the row's email was replaced at deletion (specs/112) - by design.
- Two-factor reset on a deleted account is already 409 (the secret was erased).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `UserAdminResponse` carries `DeletedAt`.
- **FR-002**: `GET /api/users` leaves deleted accounts out unless `includeDeleted=true`.
- **FR-003**: Every moderation command on a deleted account is 409 with `code: AccountDeleted`, writing nothing.
- **FR-004**: The storefront shows a Deleted badge and no actions, and a "Show deleted accounts" box.

## Success Criteria *(mandatory)*

- **SC-001**: No deleted account is listed as Active or offered an action (tested server and client side).

## Assumptions

- Deletion is final (specs/112); there is nothing for staff to undo.
