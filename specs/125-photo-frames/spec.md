# Feature Specification: Product photographs fill their frame

**Feature Branch**: `feat/251-photo-frames` | **Created**: 2026-10-01 | **Issue**: #251

**Status**: Draft

**Input**: Issue #251 - "product photographs fill their frame" - found in the screen review of 2026-10-01.

## Why

The product page and the catalogue's hero put every photograph in a square frame with `object-contain`. A camera photographed landscape - nearly all of them - sat between two bands of white, about 100px each on the product page: the biggest picture in the shop was a third empty.

## User Scenarios & Testing *(mandatory)*

### US1 - A large photograph is shown at its own shape (Priority: P2)



**Acceptance Scenarios**:

1. **Given** a product with a 3:2 photograph, **When** its page opens, **Then** the picture's box is 3:2 - no bands above and below, nothing cropped.
2. **Given** a product with no photograph, **Then** the lens tile keeps a 4:3 frame.
3. **Given** cards and thumbnails, **Then** they keep their fixed frames (4:3, square) - a grid needs equal cards.

### Edge Cases

- A very tall photograph is capped (`max-h`) so it cannot push the price below the fold; inside the cap it keeps its shape.
- While a large photograph loads its frame is 4:3 (and tinted, specs/122), then takes the photograph's shape.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Large images (the product page, the catalogue hero, the seller's preview) are drawn at their natural aspect ratio, width-filling, capped in height, with no fixed frame.
- **FR-002**: The missing-photograph tile is 4:3 when large.
- **FR-003**: A browser check uploads a 3:2 photograph and asserts the product page draws it 3:2.

## Success Criteria *(mandatory)*

- **SC-001**: On the product page a landscape photograph has no bands (checked in a browser with an uploaded 3:2 image).
- **SC-002**: The check fails with the square frame.

## Assumptions

- Cropping (`object-cover`) is not acceptable for product photographs: an edge of the camera, or the text of a spec sheet, is what would be cut.
