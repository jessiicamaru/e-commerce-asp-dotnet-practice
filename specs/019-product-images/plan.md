# Implementation Plan: Product Images

**Branch**: `019-product-images` | **Date**: 2026-09-22 | **Spec**: [spec.md](spec.md)

## Summary

Each product gets at most one image:

- administrators upload it (`PUT`) and remove it (`DELETE`);
- anyone fetches it (`GET`), through a versioned address that is safe to cache forever;
- the bytes live outside the database, in an `IProductImageStore` whose first implementation is a
  directory on a named volume.

Replacement writes the new file, switches the row with a guarded update, and only then deletes the
old file. The storefront shows the picture in place of the letter tile.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript 6 + React 19 (client)
**Primary Dependencies**: ASP.NET Core (multipart binding, `RequestSizeLimit`), EF Core + Npgsql, MediatR, FluentValidation
**Storage**: PostgreSQL (two nullable columns on `products`); the filesystem for the bytes (named volume `catalog_images`)
**Testing**: xUnit against real PostgreSQL, in `Ecommerce.Catalog.Tests`, with the real file store in a temp directory; Bruno; curl through the gateway
**Target Platform**: Linux containers (compose), Windows dev host
**Constraints**: 2 MB per image; JPEG/PNG/WebP only; one Catalog instance
**Scale/Scope**: one Catalog service change, one migration, one client change, Bruno requests

## Constitution Check

| Principle | Check | Result |
| :-- | :-- | :-- |
| I. Service autonomy | Catalog owns its images and store; no other service reads them. No new contract. | Pass |
| II. Clean Architecture | The store interface is in Application; the filesystem implementation is in Infrastructure; controllers only `Mediator.Send`. | Pass |
| III. Atomic writes | No message is published. The row switch is one guarded statement; file and row ordering are D3. A file system and a database cannot share a transaction, so ordering, not atomicity, is what keeps FR-007. | Pass, with the ordering justified in research D3 |
| IV. Identity from the token | Admin role from the token via `[Authorize(Roles="Admin")]`; no user id in any request. | Pass |
| V. Evidence over assumption | Negative controls planned: type detection, guard, ordering. | Pass |
| Schema evolution | Two nullable columns plus CHECKs; additive. | Pass |
| Config fails at startup | The store root must be writable at startup (D8). | Pass |
| Every service has `/health` | Unchanged. | Pass |

No violations, so Complexity Tracking is empty.

## Project Structure

```text
server/src/Services/Catalog/
  Ecommerce.Catalog.Domain/Entities/Product.cs                     + ImageContentType, ImageUpdatedAt
  Ecommerce.Catalog.Application/Common/Interfaces/IProductImageStore.cs    new
  Ecommerce.Catalog.Application/Products/Images/                   new: ImageFormat, ProductImageKey,
      UploadProductImage/ (command, validator, handler), RemoveProductImage/, GetProductImage/
  Ecommerce.Catalog.Application/Products/Common/ProductResponse.cs + ImageUrl (and one mapping helper)
  Ecommerce.Catalog.Application/Common/Interfaces/IProductRepository.cs    + TrySetImageAsync (guarded)
  Ecommerce.Catalog.Infrastructure/Images/FileSystemProductImageStore.cs    new
  Ecommerce.Catalog.Infrastructure/Configurations/ProductConfiguration.cs   + columns, CHECKs
  Ecommerce.Catalog.Infrastructure/Migrations/<ts>_AddProductImage.cs       new
  Ecommerce.Catalog.WebApi/Controllers/ProductsController.cs       + PUT/DELETE/GET image
server/tests/Ecommerce.Catalog.Tests/ProductImageTests.cs          new
server/Dockerfile                                                  /app/data owned by the app user
server/docker-compose.app.yml                                      catalog_images volume
client/src/api/catalog.ts, pages/CatalogPage.tsx, pages/ProductPage.tsx   show the image
bruno/fixtures/*, bruno/product/*image*.yml, bruno/security-checks/*image*.yml
```

## Design artifacts

[research.md](research.md), [data-model.md](data-model.md), [contracts/api.md](contracts/api.md),
[quickstart.md](quickstart.md)
