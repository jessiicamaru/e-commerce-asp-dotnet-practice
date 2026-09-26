# HTTP Contract: What hangs on a product off the shelf

> Written on 2026-09-27, after the feature merged (#169), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature**: [spec.md](../spec.md)

No endpoint was added. Four existing reads changed their answers, and the image address in every product response
gained a parameter. All go through the gateway on `:5000`, under `catalog-products-route`
(`/api/products/{**catch-all}`), unchanged. No message or gRPC contract changed.

Errors follow the project's RFC 7807 shape through `GlobalExceptionHandler`.

---

## The image address in product responses

`ProductResponse.imageUrl` and each `VariantResponse.imageUrl` (when the variant has its own photograph):

```text
before: /api/products/{id}/image?v=639258012345678901
after:  /api/products/{id}/image?v=639258012345678901&k=5d1c9e0b7a8f4e2b9c3d6a1f0e2b4c8d
        /api/products/{id}/variants/{variantId}/image?v=...&k=...
```

`v` is `ImageUpdatedAt` in ticks (`ProductImageKey.Version`, unchanged); `k` is the image's `ImageAccessKey` as 32 lowercase hex digits (`Guid` format `N`). The address is only in
responses the reader may see: the public list and lookup (listed products), a seller's own products, the
moderators' queue. Bruno's `product/upload image` asserts `^/api/products/[0-9a-f-]+/image\?v=\d+&k=[0-9a-f]{32}$`.

---

## `GET /api/products/{id}/image` - anonymous

Query: `v` (the version, optional), `k` (the key, optional, a Guid).

| Product | `k` | Status | `Cache-Control` |
| :--- | :--- | :--- | :--- |
| Listed, has an image | absent or any | `200`, the bytes, `X-Content-Type-Options: nosniff` | `public, max-age=31536000, immutable` when `v` is current, else `no-cache` |
| Not listed, has an image | the image's own key | `200` | `private, no-cache` |
| Not listed | absent or another key | `404` (empty body) | - |
| No image, or no such product | - | `404` | - |

## `GET /api/products/{id}/variants/{variantId}/image` - anonymous

The same, deciding by the variant's product's listing and the variant's own key. A variant of another product, or
a variant with no photograph, is `404` as before.

---

## `GET /api/products/{id}/reviews` - anonymous

Query: `pageNumber`, `pageSize` (default 12). Response unchanged (`PaginatedList<ReviewResponse>`).

| Caller | Product listed | Product not listed | No such product |
| :--- | :--- | :--- | :--- |
| Anybody | `200` | `404` `Product not found.` | `404` `Product not found.` |
| Its seller | `200` | `200` | `404` |
| Admin or Moderator | `200` | `200` | `404` |

Before this feature every row of the "not listed" column was `200`.

## `GET /api/products/{id}/questions` - anonymous

The same table as reviews. Response unchanged (`PaginatedList<QuestionResponse>`).

---

## Authorization

Unchanged attributes (`[AllowAnonymous]` on all four). The decision is made in the handler, from `ICurrentUser` for
reviews and questions, and from the key for images, because an image request carries no token.
