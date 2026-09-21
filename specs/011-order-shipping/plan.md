# Implementation Plan: Somewhere for the Order to Go

**Branch**: `011-order-shipping` | **Date**: 2026-09-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/011-order-shipping/spec.md` — issue
[#20](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/20)

## Summary

Customers keep delivery addresses in **Identity**. Checkout names an address and a delivery option;
**Order** reads the address from Identity over gRPC with the customer's forwarded token — the pattern
Cart established — and freezes a copy of it, and of the option's name and price, onto the order. The
total, and therefore the payment, includes delivery. After payment the order reads **Paid**; an
Admin moves it to **Preparing** and then **Shipped** with a tracking reference, through guarded
transitions. No message contract changes.

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: ASP.NET Core, EF Core + Npgsql, MediatR 12.4.1, FluentValidation 12.1.1,
MassTransit 8.3.6, Grpc.AspNetCore / Grpc.Net.Client (already in use by Catalog, Cart, Order)

**Storage**: PostgreSQL 16 — Identity (`5435`) gains `delivery_addresses`; Order (`5434`) gains
nullable columns on `orders`

**Testing**: xUnit against real PostgreSQL — new `Ecommerce.Identity.Tests`, extended
`Ecommerce.Order.Tests`; `verify-saga.sh`, `verify-auth.sh`, the Bruno collection

**Target Platform**: Linux containers and the host path (`start-dev`)

**Project Type**: microservices web backend

**Performance Goals**: checkout gains one more synchronous read; it must stay within the existing
retry budget (3 attempts, 200 ms / 1 s)

**Constraints**: no contract change; additive schema only; identity from the token everywhere

**Scale/Scope**: 2 services changed, 1 new test project, ~9 endpoints, 1 proto

## Constitution Check

*Checked before research and again after design.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | ✅ Identity alone owns addresses; Order holds a **copy**, never joins back, and decides nothing from Identity's data after checkout. The address is read through Identity's own API, not its database. Delivery options are owned by Order alone. |
| **II. Layering** | ✅ `IAddressReader` declared in Order.Application, implemented in Order.Infrastructure; address CQRS in Identity.Application; gRPC service and controllers in WebApi. |
| **III. Atomic writes / idempotency** | ✅ The address read happens **before** anything is staged, like the cart and price reads. Fulfilment transitions are guarded single-statement updates; a repeat affects zero rows. No new consumer; `CompleteOrder` keeps its guard and changes only its target value. |
| **IV. Identity from the token** | ✅ No command, route or gRPC request carries a user id. Order forwards the caller's token to Identity. Fulfilment is `Admin` only. |
| **V. Evidence** | ✅ One-default, the 20-limit and ownership tested against a real database; the charge (not only the order row) asserted end to end; cross-customer checks with real signed tokens; a negative control on the fulfilment guard. |
| **Schema evolution** | ✅ Additive: a new table, nullable columns, new enum strings. The `Completed → Paid` data update is readable by the previous image (research D3). |
| **Configuration** | ✅ Delivery options validated at startup; `IDENTITY_GRPC_ADDRESS` defaulted like Cart's and Catalog's. |
| **Service topology** | ⚠️ Identity gains a second port, as Catalog and Cart have. Not a violation — the topology rule is about the HTTP port — but it is Identity's first change of shape, and its `app.Run(url)` fallback goes (research D5). |

**Post-design re-check**: no violations. Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/011-order-shipping/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/api.md
├── checklists/requirements.md
└── tasks.md            (speckit-tasks)
```

### Source Code

```text
server/src/BuildingBlocks/Ecommerce.Contracts.Grpc/Protos/
└── address_reading.proto                          NEW

server/src/Services/Identity/
├── Ecommerce.Identity.Domain/Entities/DeliveryAddress.cs              NEW
├── Ecommerce.Identity.Application/Addresses/
│   ├── Common/AddressResponse.cs, Interfaces/IAddressRepository.cs    NEW
│   ├── Commands/{SaveAddress,UpdateAddress,DeleteAddress,SetDefaultAddress}/   NEW
│   └── Queries/{GetMyAddresses,GetMyAddress}/                         NEW
├── Ecommerce.Identity.Infrastructure/
│   ├── Configurations/DeliveryAddressConfiguration.cs                NEW
│   ├── Persistence/Repositories/AddressRepository.cs                 NEW
│   └── Migrations/<ts>_AddDeliveryAddresses.cs                        NEW
└── Ecommerce.Identity.WebApi/
    ├── Program.cs                     both Kestrel endpoints; AddGrpc; MapGrpcService
    ├── Controllers/AddressesController.cs                             NEW
    └── Grpc/AddressReadingService.cs                                  NEW

server/src/Services/Order/
├── Ecommerce.Order.Domain/  Order.cs (+ShippingAddress owned type, shipping fields), OrderStatus (+Preparing, Shipped)
├── Ecommerce.Order.Application/
│   ├── Common/Interfaces/IAddressReader.cs, IShippingOptions.cs       NEW
│   ├── Orders/Commands/SubmitOrder/   (+AddressId, ShippingOption; validator)
│   ├── Orders/Commands/CompleteOrder/ (Completed → Paid)
│   ├── Orders/Commands/{PrepareOrder,ShipOrder}/                      NEW
│   └── Orders/Queries/{GetShippingOptions,GetOrdersForFulfilment}/    NEW
├── Ecommerce.Order.Infrastructure/
│   ├── Identity/GrpcAddressReader.cs                                  NEW
│   ├── Shipping/ConfiguredShippingOptions.cs                          NEW
│   ├── Persistence/Repositories/OrderRepository.cs  (+TryAdvanceAsync)
│   └── Migrations/<ts>_AddShippingToOrders.cs                         NEW
└── Ecommerce.Order.WebApi/Controllers/OrdersController.cs  (body, options, fulfilment)

server/tests/
├── Ecommerce.Identity.Tests/                                          NEW
└── Ecommerce.Order.Tests/  (+fulfilment, +freezing, +Paid)

server/  Dockerfile (Identity.Tests is not in images — no line), docker-compose.app.yml (6056, IDENTITY_GRPC_ADDRESS),
         ApiGateway appsettings.json (/api/addresses routes), Ecommerce.slnx
.github/  workflows/ci.yml (Identity.Tests DB, IDENTITY_GRPC_* env), scripts/verify-saga.sh, verify-auth.sh
bruno/    addresses/, order checkout body, admin fulfilment, security checks
docs/ + CLAUDE.md + specs/003-order-lifecycle/data-model.md
```

**Structure Decision**: No new service. Identity and Order change inside their existing four
projects; the only new project is `Ecommerce.Identity.Tests`.

## Complexity Tracking

Empty — no violation to justify.
