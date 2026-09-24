# gRPC

> **Generated** by [`docs/tools/generate_reference.py`](../tools/generate_reference.py) from commit `c42d85b`. Do not edit by hand - change the code and run the script again.

The synchronous calls between services. Each runs over h2c on the serving service's second port - one plaintext port cannot carry HTTP/1.1 and HTTP/2 (see [service-to-service communication](../architecture/service-to-service-communication.md)). Every call is asked live: none of them is cached.

## `AddressReading` (address_reading.proto)

Served by **Identity**, called by **Order**.

| RPC | Request | Response |
| :-- | :-- | :-- |
| `GetMyAddress` | `GetMyAddressRequest` | `GetMyAddressResponse` |

## `CartReading` (cart_reading.proto)

Served by **Cart**, called by **Order**.

| RPC | Request | Response |
| :-- | :-- | :-- |
| `GetMyCart` | `GetMyCartRequest` | `GetMyCartResponse` |

## `CatalogOwnership` (catalog_ownership.proto)

Served by **Catalog**, called by **Inventory**.

| RPC | Request | Response |
| :-- | :-- | :-- |
| `GetVariantOwners` | `GetVariantOwnersRequest` | `GetVariantOwnersResponse` |

## `CatalogPricing` (catalog_pricing.proto)

Served by **Catalog**, called by **Cart, Order**.

| RPC | Request | Response |
| :-- | :-- | :-- |
| `GetPrices` | `GetPricesRequest` | `GetPricesResponse` |
| `DescribeProducts` | `DescribeProductsRequest` | `DescribeProductsResponse` |
| `PriceVariants` | `PriceVariantsRequest` | `PriceVariantsResponse` |
| `DescribeVariants` | `DescribeVariantsRequest` | `DescribeVariantsResponse` |
