# Feature Specification: Product Images

**Feature Branch**: `019-product-images`
**Created**: 2026-09-22
**Status**: Draft
**Input**: Issue [#45](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/45), with
decisions taken on the owner's behalf (they asked for the recommended option instead of being asked).

## Why this exists

No product has a picture. The storefront (#36) shows a letter on a grey tile instead, because nothing
in Catalog stores or serves an image.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An administrator gives a product its picture (Priority: P1)

An administrator uploads a photo for a product. From then on, every shopper who browses the catalogue
or opens that product sees it.

**Why this priority**: Without a way to attach an image, nothing else in this feature has anything to
show.

**Independent Test**: Upload a PNG to an existing product as an administrator, then fetch the product.
It carries an image address, and that address returns the same bytes with an image content type.

**Acceptance Scenarios**:

1. **Given** a product with no image, **When** an administrator uploads a JPEG, PNG or WebP file of
   at most 2 MB, **Then** the product's image address is set and serves that file.
2. **Given** a product with an image, **When** an administrator uploads a different one, **Then** the
   image address changes and serves the new file, and the old file is no longer kept.
3. **Given** a customer or an anonymous caller, **When** they try to upload, **Then** they are refused
   (403 / 401) and nothing changes.

---

### User Story 2 - Shoppers see the picture (Priority: P1)

The listing and the product page show the product's image where they show a placeholder today. They
keep the placeholder for products that have none.

**Why this priority**: This is what the issue is about. An image that is stored but never shown
changes nothing for a shopper.

**Independent Test**: With one product that has an image and one that does not, open the listing. The
first shows its picture and the second shows the placeholder.

**Acceptance Scenarios**:

1. **Given** a product with an image, **When** a shopper opens the listing or the product page,
   **Then** the image is shown, without signing in.
2. **Given** a product without an image, **When** it is shown, **Then** the placeholder appears and
   nothing is broken.
3. **Given** an image that was just replaced, **When** a shopper's browser had cached the old one,
   **Then** they see the new one, because the address itself changed.

---

### User Story 3 - Bad uploads are refused (Priority: P2)

A file that is not an image, is an unsupported kind of image, or is too large is refused with a
message that says why. The product keeps whatever image it had.

**Why this priority**: An upload endpoint that accepts anything is a way to fill the disk or to serve
arbitrary content from the shop's own address.

**Independent Test**: Upload a text file labelled `image/png`, and separately a 3 MB file. Both are
refused with 400, and the product is unchanged.

**Acceptance Scenarios**:

1. **Given** a file whose content is not JPEG, PNG or WebP, **When** it is uploaded with an image
   content type, **Then** it is refused. The file's own bytes decide its type, not the label.
2. **Given** a file larger than 2 MB, **When** it is uploaded, **Then** it is refused.
3. **Given** any refused upload, **Then** the product's existing image, if any, is unchanged, and no
   new file remains in storage.

---

### User Story 4 - An administrator removes a picture (Priority: P3)

An administrator removes a product's image, and the product goes back to the placeholder.

**Why this priority**: Rare, but without it a wrong image can only ever be replaced, never taken down.

**Acceptance Scenarios**:

1. **Given** a product with an image, **When** an administrator removes it, **Then** the product has
   no image address and the stored file is gone.
2. **Given** a product without an image, **When** removal is requested, **Then** it succeeds quietly.

### Edge Cases

- **Unknown product**: uploading to, removing from, or fetching the image of a product that does not
  exist is 404.
- **Storage fails midway**: if the file cannot be written, the product keeps its previous image. If
  the old file cannot be deleted after a successful replacement, the product still shows the new one;
  the leftover file is waste, not an error.
- **Two administrators replace the same image at once**: exactly one replacement wins. The other is
  told the image changed meanwhile (409), and its file is not left behind.
- **An address for an image that has since changed**: it still resolves to the current image. Only
  the address that matches the current version may be cached for a long time.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A product has at most one image.
- **FR-002**: Only an administrator can attach, replace or remove a product's image.
- **FR-003**: Anyone, signed in or not, can fetch a product's image.
- **FR-004**: A product, as returned by the listing and by the product lookup, carries a nullable image
  address. It is absent (null) when there is no image, and it changes whenever the image does.
- **FR-005**: Only JPEG, PNG and WebP up to 2 MB are accepted. The type is determined from the file's
  content, never from the declared content type or the file name.
- **FR-006**: The image is served with the content type determined at upload.
- **FR-007**: At no moment does replacing an image leave the product pointing at a file that does not
  exist, including when a step fails.
- **FR-008**: A refused or failed upload leaves the product exactly as it was.
- **FR-009**: Image bytes are not stored in the database.
- **FR-010**: The storefront shows the image on the listing and the product page, and the placeholder
  when there is none.
- **FR-011**: Every new endpoint has Bruno requests: upload, fetch, oversized, wrong type.

### Key Entities

- **Product image**: at most one per product. It is recorded on the product as the stored image's
  content type and the time it last changed. Together these identify the stored file and form the
  version in its address. The bytes live in an image store outside the database.

## Success Criteria *(mandatory)*

- **SC-001**: An administrator can give a product a picture in one request, and a shopper sees it on
  the next page load.
- **SC-002**: Every upload whose content is not JPEG, PNG or WebP is refused, whatever its declared
  type.
- **SC-003**: After any refused or failed upload, the product's image address still resolves.
- **SC-004**: A shopper whose browser cached the previous image sees the replacement without clearing
  anything.

## Assumptions

- There is one Catalog instance. The first store is a directory on disk (a named volume in
  containers), which two instances would not share. **Recorded, not solved**: object storage is the
  real answer, and the store is the seam where it plugs in, as `StubPaymentGateway` is for Payment.
- No resizing or thumbnails; the client scales.
- No admin interface in the storefront; administrators upload through the API (and Bruno).
