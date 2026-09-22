# Research: Product Images

Decisions the input already settled are restated in one line each. The research is in the ones
building it raised.

## D1 - Where the bytes live: a store behind a seam

**Decision**: `IProductImageStore` in Catalog.Application (`SaveAsync`, `OpenReadAsync`,
`DeleteAsync`, keyed by a string). `FileSystemProductImageStore` in Catalog.Infrastructure, rooted at
`ProductImages:Root`.

**Why**: Bytes in PostgreSQL would bloat every backup and every replica with blobs the database never
queries. Object storage is the real answer, but adding MinIO would add a tenth container to a
practice stack for one feature. The seam lets that swap happen without touching the Application
layer, as `StubPaymentGateway` does for Payment.

**Rejected**: `bytea` column (FR-009 forbids it); MinIO now (cost without a second consumer).

**Limitation, recorded**: one Catalog instance. Two would each have their own directory.

## D2 - The store is keyed by the row, so the row decides what exists

**Decision**: file key = `{productId:N}-{ImageUpdatedAt ticks}.{ext}`, derived from the row. There is
no separate file-name column.

**Why**: One source of truth. The version in the URL, the file on disk and the cache key are all the
same number, so none of them can disagree with the others.

## D3 - Replace order: write, switch, then delete

**Decision**:

1. Write the new file under a new key.
2. Switch the row with a **guarded** single statement:
   `UPDATE products SET ImageContentType=@t, ImageUpdatedAt=@new WHERE Id=@id AND ImageUpdatedAt IS NOT DISTINCT FROM @seen`.
3. Only if that affected one row, delete the old file. If it affected zero, delete the *new* file and
   answer 409.

**Why**: FR-007. At every point the row names a file that exists. A crash after step 1 leaves an
orphan file (waste). A crash after step 2 leaves the old file (waste). Neither leaves a dangling
pointer. The guard makes concurrent replacements safe: without it, two writers could each delete the
file the other had just installed.

**Rejected**: delete-then-write (a failure in between leaves a product pointing at nothing);
last-write-wins without a guard (orphans, and a dangling pointer under interleaving).

## D4 - Type is decided by the leading bytes

**Decision**: read the first 12 bytes.

- JPEG: `FF D8 FF`
- PNG: `89 50 4E 47 0D 0A 1A 0A`
- WebP: `52 49 46 46 ?? ?? ?? ?? 57 45 42 50` ("RIFF" .... "WEBP")

Anything else is 400. The client's `Content-Type` and the file name are ignored.

**Why**: A header is a claim. Serving an HTML or SVG file under the shop's own origin as if it were an
image is how an upload endpoint becomes stored XSS. **SVG is deliberately excluded**: it is an image
format that can carry script.

## D5 - Size limit enforced twice

**Decision**: `[RequestSizeLimit(2 MB + 64 KB)]` on the action, so an enormous body is refused before
it is buffered. *Planned as 413; measured as **400*** - MVC turns the failed form read into a model
state error (`"Request body too large. The max request body size is 2162688 bytes."`). The protection
is what matters and it holds: a 50 MB body is answered in 0.05 s. The validator then refuses content over exactly 2 MB with a 400 that says so.

**Why**: The attribute protects the server; the validator produces the message a client can act on.

## D6 - Serving and caching

**Decision**:

- `imageUrl = /api/products/{id}/image?v={ticks}`.
- `GET` with a matching `v` gets `Cache-Control: public, max-age=31536000, immutable`.
- `GET` without `v`, or with a stale one, gets the current image with `Cache-Control: no-cache`.
- `X-Content-Type-Options: nosniff` always.

**Why**: SC-004. The address changes when the image changes, so a long-lived cache is safe exactly
for the address that names the current version. `nosniff` stops a browser second-guessing the
declared type.

## D7 - Named volume needs the directory to exist in the image

**Decision**: the Dockerfile creates `/app/data`, owned by `$APP_UID`, before switching user. Compose
mounts the named volume `catalog_images` **at `/app/data` itself**. The store creates
`product-images/` inside it.

**Why**: the container runs as a non-root user. Docker initialises a *new* named volume from the
image's directory, ownership included. A mount point the image does not have is created root-owned.

**What happened**: the first attempt mounted the volume at `/app/data/product-images`, which is a
path the image lacks. The volume came up root-owned, and Catalog refused to start:
`Product images cannot be stored: '/app/data/product-images' is not a writable directory`. That was
the D8 check doing its job. Mounting on `/app/data` gave `drwxr-xr-x app app /app/data/product-images`.
A volume that has already been created keeps its ownership, so the empty root-owned one had to be
removed once. Creating an empty `/app/data` in the shared image costs the other six services
nothing.

## D8 - Startup check

**Decision**: the file store creates its root at startup and fails startup if the root is not
writable.

**Why**: the constitution requires a service to fail at startup, not on each request, when something
it needs is missing. An unwritable volume would otherwise surface as a 500 on the first upload.

## D9 - Database invariants

**Decision**: two nullable columns, `ImageContentType varchar(20)` and `ImageUpdatedAt timestamptz`,
with two CHECK constraints:

- `CK_products_image_complete`: both columns are null, or both are set;
- `CK_products_image_type`: the type is null or one of `image/jpeg`, `image/png`, `image/webp`.

**Why**: the constitution requires invariants that matter to be expressed in the database. Both
columns are nullable and have no default, so the change is additive and an earlier image runs
against it.

## D10 - Bruno fixtures

**Decision**:

- `bruno/fixtures/`: a real 1x1 PNG;
- a text file for the wrong-type case;
- `too-big.png`: a PNG signature followed by 2 MB + 10 KB of zeros. It is over the image limit but
  under the request ceiling, so it reaches the validator. It is a few KB in git.

Requests use Bruno's `multipart-form` body with a `type: file` entry.
