# Feature Specification: A seller is told when a variant runs low

**Feature Branch**: `102-low-stock-notice` | **Created**: 2026-09-27 | **Issue**: #200

**Status**: Merged (#209, 2026-09-27)

**Input**: Issue #200 - "a seller is not told when a variant runs low".

## Why

A seller finds out that something sold out when a shopper cannot buy it. Nothing warns them while there is still time
to restock. Inventory tells nobody anything (`grep NotifyAsync server/src/Services/Inventory` finds nothing). The
seller's list shows the count, but only when they look.

## User Scenarios & Testing *(mandatory)*

### US1 - A sale that takes a variant below its line tells the seller once (Priority: P1)

The seller is told "“Fujifilm X-T5 · Colour: Silver” is running low: 4 left" when a checkout takes the variant's
available stock from at or above its threshold to below it. Further sales while it stays below say nothing. Once it is
back at or above the line (restocked, or held units returned), the next crossing tells them again.

**Why this priority**: This is the issue.

**Independent Test**: Take a variant with 6 available and a threshold of 5. Order 2: the seller receives one notice.
Order 1 more: no second notice.

**Acceptance Scenarios**:

1. **Given** 6 available and a threshold of 5, **When** a checkout reserves 2, **Then** the seller is told once, with
   4 left.
2. **Given** 4 available (already below), **When** a checkout reserves 1, **Then** nobody is told.
3. **Given** a variant that crossed, then went back to 6 when the payment failed, **When** a checkout takes it to 4
   again, **Then** the seller is told again - it is a new crossing.
4. **Given** the same reservation delivered twice by the broker, **Then** one notice, because the second delivery
   changes no stock.
5. **Given** a product of the shop itself (no seller), **Then** nobody is told.

---

### US2 - A seller chooses the line per variant (Priority: P2)

Every variant starts at the shop's default threshold (`Inventory:LowStock:DefaultThreshold`, 5). A seller can set a
variant's own threshold, or turn the notice off with 0, next to its stock count. Clearing the field puts the variant
back on the default.

**Why this priority**: The default works for most variants. A variant that sells ten a day or one a month needs its own
line.

**Acceptance Scenarios**:

1. **Given** a seller's variant, **When** they set its threshold to 10, **Then** the notice fires below 10 for that
   variant only.
2. **Given** a threshold of 0, **Then** the variant never sends the notice.
3. **Given** another seller's variant, **Then** setting its threshold is the same 404 as setting its stock
   (specs/031).
4. **Given** a threshold below 0 or above 100,000, **Then** 400.

### Edge Cases

- **A seller lowering their own stock** below the line through `PUT /api/stock/{id}` is told nothing. They did it.
- **Held units that come back** (expiry, a failed payment, a cancellation) raise availability. They send nothing, and
  they make the next fall below the line a new crossing.
- **Exactly at the threshold.** "Below" means strictly below: 5 available with a threshold of 5 is not low.
- **A sale straight from 6 to 0** (sold out) is still one crossing and one notice, with 0 left.
- **Several variants in one order** are judged separately, and each that crosses tells its seller once.
- **Variants from before this feature** have no threshold of their own, so the default applies. No backfill is needed.
- **Staff opt-in for the shop's own products** is not built. There is no single person to tell for "the shop" (research
  D5).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `stock_items.LowStockThreshold` (int, null = the shop default) and configuration
  `Inventory:LowStock:DefaultThreshold` (default 5, validated 0-100,000 at startup).
- **FR-002**: The reservation path compares each variant's available stock before and after, inside its `FOR UPDATE`
  transaction. When a variant goes from ≥ its threshold to < it, and the threshold is above 0, it stages
  `StockRanLowEvent(VariantId, QuantityAvailable, Threshold, OccurredAt)` through the outbox in the same transaction.
  No other path publishes it.
- **FR-003**: Catalog consumes `StockRanLowEvent`. For a variant with a seller, it notifies that seller with kind
  `StockRunningLow`, data `product` (the product name plus the variant's option summary) and `left`, linking to
  `/shop/products/{productId}`. For the shop's own products, and for unknown variants, it does nothing.
- **FR-004**: `PUT /api/stock/{variantId}/low-stock-threshold` (Seller, Admin), body `{ "threshold": int | null }`. It
  uses the same ownership check as setting stock. Stock responses carry `lowStockThreshold`, the effective value, and
  `lowStockThresholdIsDefault`.
- **FR-005**: A new placeholder `left` (data key `left`) and the kind `StockRunningLow` are declared in
  `notification-kinds.json`, with words in en and vi.
- **FR-006**: The seller's stock editor shows and sets the threshold.

### Key Entities

- **Stock item** (Inventory): gains the variant's own low-stock threshold.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Inventory tests cover:
  - 6 → 4 with a threshold of 5 publishes one event; 4 → 3 publishes none;
  - a return to 6 followed by 6 → 4 publishes again;
  - a threshold of 0 never publishes; a variant's own threshold overrides the default;
  - setting stock below the line publishes nothing; a redelivered reservation publishes nothing;
  - the threshold endpoint refuses another seller's variant (404) and an out-of-range value (400).
- **SC-002**: Catalog tests: a seller is notified with the product, the variant and the count left, conforming to the
  kinds file; the shop's own product and an unknown variant notify nobody.
- **SC-003**: Storefront tests: the stock editor shows the effective threshold and sends a new one or a clear.
- **SC-004**: Bruno: a seller sets a variant's threshold and reads it back; another seller's variant is 404; without
  a token is 401.
- **SC-005**: Each of these mutations makes a test fail: the crossing compared with ≤ instead of <, the before value
  ignored (every low sale publishes), threshold 0 treated as active, the override ignored, and Catalog notifying for
  the shop's own products.

## Decision

1. **Inventory decides the crossing and Catalog tells the seller**, through an event. There is no synchronous call and
   no seller id on the stock row ([research.md](research.md) D1).
2. **Stateless crossing**: the before and after values come from the same locked transaction, with no stored "low" flag
   (D2).
3. **A sale is the reservation**, because that is when units stop being sellable (D3).

## Assumptions

- Five units is a sensible default line for a small shop's cameras and lenses. It is configuration, not code.

## Out of scope

- An email for the notice; staff notices for the shop's own products; a daily digest of low stock.
