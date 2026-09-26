# Contracts: Seed Photographs

> Written on 2026-09-27, after the feature merged (#69), from the code at that merge, the pull request and server/seed/README.md.

**Feature**: [spec.md](../spec.md)

**This feature changed no interface** - no endpoint, message, gRPC service or table. It is a client of three
existing endpoints, all through the gateway (`GATEWAY_URL`, default `http://localhost:5000`):

| Call | Owner | Used for | Expected |
| :--- | :--- | :--- | :--- |
| `POST /api/auth/login` `{email, password}` | Identity | The administrator's token, read from `token` | `200`; anything else stops the script with the `detail` |
| `GET /api/products?pageSize=200` | Catalog | The SKU → product map, and whether each already has `imageUrl` | `200`; one page only |
| `PUT /api/products/{id}/image` | Catalog, `Seller,Admin` | The upload: `multipart/form-data`, one part named `file` carrying the bytes and the type the script detected | `200` with the product; a refusal is printed with its status and `detail` and counted failed |

Catalog's own rules for the upload (specs/019) are the authority: JPEG, PNG or WebP by content, at most
2 MB (`ProductImageKey.MaxBytes`), SVG refused. The script repeats the type and size checks only to refuse
early.
