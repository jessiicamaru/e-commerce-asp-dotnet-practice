# Feature Specification: Staff counts never share a list's cache

**Feature Branch**: `fix/268-moderation-count-keys` | **Created**: 2026-10-02 | **Issue**: #268

**Status**: Draft

**Input**: Issue #268 - "the moderation dashboard caches a one-row page under the review list's key" - found while building specs/129 (#246).

## Why

The moderation dashboard and the overview counted what waits by reading each queue with a page of one - under the same query key as the queue's own list, which carries the status and the page but not the size. Opening `/admin/products` after either showed the cached one-row page until the refetch replaced it: a list that flickers from one product to twelve. And a decision - approving a product, a shop - refreshed the lists but not the sidebar's counts from specs/129.

## User Scenarios & Testing *(mandatory)*

### US1 - A queue's page is its own, and its count follows a decision (Priority: P2)



**Acceptance Scenarios**:

1. **Given** the moderation dashboard or the overview was opened, **When** the products queue opens, **Then** it shows its own page from the first render - never the one-row page.
2. **Given** a product or a shop application decided, **Then** the counts on the sidebar, the dashboard and the overview are read again.

### Edge Cases

- The counts are one cache now: the sidebar, the moderation dashboard and the overview read the same `staff-waiting` entries, so they agree and cost one request each.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The moderation dashboard and the overview read their counts from `useStaffWaiting`.
- **FR-002**: The review-queue and shop-application keys carry the page size, so a page of a different size is never shared.
- **FR-003**: Deciding a product or a shop application invalidates `staff-waiting`.

## Success Criteria *(mandatory)*

- **SC-001**: Opening the products queue after the dashboard shows twelve rows from the first render (tested).

## Assumptions

- No server change.
