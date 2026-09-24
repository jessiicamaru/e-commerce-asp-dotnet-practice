# E-Commerce Documentation

Documentation for a camera marketplace built as a .NET 10 microservices system - eight services behind
a YARP gateway, one PostgreSQL database each, RabbitMQ between them, a saga orchestrating checkout - with
a React storefront. Start with the [project overview](overview/project-overview.md).

Everything here describes the system **as it runs**. The reasoning behind each feature - what was
decided, what was rejected and why - is in its design record under [`specs/`](../specs/); the
[decision log](project/decisions.md) indexes the important ones.

## How this documentation is organised

| Folder | What is in it | Read it to |
| :-- | :-- | :-- |
| [`overview/`](overview/) | What the project is, who uses it, its scope and status; a glossary | Understand the project in ten minutes |
| [`architecture/`](architecture/) | How the system is put together: services, communication, the saga, messaging, errors, the storefront; ADRs | Understand the design |
| [`features/`](#features) | One document per business area: what people can do, how it works, the rules and why, data, API, tests | Understand or change a feature |
| [`reference/`](reference/) | Every endpoint, message, gRPC call, table and gateway route - **generated from the code** | Look something up exactly |
| [`testing/`](testing/) | The testing strategy, what each layer catches, the numbers | Trust a change, or add a test |
| [`project/`](project/) | The timeline of what was built, the decision log, the backlog | Follow the project's history and what is next |
| [`guides/`](guides/) | Running the system, observability, troubleshooting | Get it running, or unstuck |
| [`infrastructure/`](infrastructure/) | Databases, the broker, containers | Operate the pieces underneath |
| [`concepts/`](concepts/) | Study notes on designs that were **not** adopted | Learn the idea, not the code |

## Overview

- [**Project overview**](overview/project-overview.md): what the system is, its principles, who uses it,
  the technology, the numbers, the status.
- [**Glossary**](overview/glossary.md): the terms used throughout - variant, part, sale, payout, stage
  callback, read model and the rest.

## Architecture

- [**Microservices design**](architecture/microservices-design.md): each service as built -
  responsibility, database, entities - the topology and the synchronous calls.
- [**How services talk to each other**](architecture/service-to-service-communication.md): messaging
  first; the four gRPC calls, why each is synchronous, and why gRPC runs on a second port.
- [**Saga orchestration & roadmap**](architecture/saga-orchestration-roadmap.md): the checkout saga, its
  compensation, and the phases that built the system.
- [**Reliable messaging & the outbox**](architecture/reliable-messaging-and-outbox-pattern.md): the
  transactional outbox, idempotent consumers, the stage-callback pattern.
- [**Error handling & `Ecommerce.Shared`**](architecture/error-handling-and-shared-building-block.md):
  RFC 7807 responses, the exception-to-status map, validation, and the shared building blocks.
- [**Storefront**](architecture/storefront.md): how the React client fits in, its layers, every page by
  role.
- [**ADR-001: UUID v7 primary keys**](architecture/adr-001-uuidv7-primary-keys.md)
- [**ADR-002: Prices exclude tax**](architecture/adr-002-tax-exclusive-prices.md)

## Features

| Area | Document |
| :-- | :-- |
| Accounts, sign-in, tokens | [JWT setup](features/auth/jwt-setup.md), [token storage & refresh](features/auth/security-best-practices.md), [database schema](features/auth/db-design.md), [CQRS guide](features/auth/cqrs-guide.md) |
| The catalogue: products, variants, images, languages, currencies, review before sale, views | [Catalog](features/catalog.md) |
| Cart, checkout, pricing and totals, the saga, settlement | [Shopping and checkout](features/shopping-and-checkout.md) |
| Parcels per seller, shipping, cancellation, delivery confirmation | [Fulfilment and delivery](features/fulfilment-and-delivery.md) |
| Sellers, shop applications, seller stock and sales, commission and payouts | [Marketplace](features/marketplace.md) |
| Staff roles, locks and bans, the moderation queues, the admin console | [Moderation and staff](features/moderation-and-staff.md) |
| The audit log and in-app notifications | [Audit and notifications](features/audit-and-notifications.md) |
| Ratings and reviews | [Ratings and reviews](features/ratings-and-reviews.md) |
| The administrator's Overview | [Admin insights](features/admin-insights.md) |

## Reference (generated)

- [**HTTP API**](reference/api.md) - every endpoint, per service, with who may call it.
- [**Messages**](reference/messages.md) - every integration message, its publishers and consumers.
- [**gRPC**](reference/grpc.md) - every synchronous call, who serves it and who calls it.
- [**Data model**](reference/data-model.md) - every table and column, per service database.
- [**Gateway routes**](reference/gateway.md) - what the gateway forwards, and where.

## Testing

- [**Testing strategy**](testing/testing-strategy.md) - integration tests against real PostgreSQL,
  storefront unit tests, the Bruno collection, the end-to-end scripts, mutation checks and CI.

## Project

- [**Timeline**](project/timeline.md) - everything built, feature by feature, with its design record and
  pull request.
- [**Decision log**](project/decisions.md) - the decisions that shape the system and where each is
  argued.
- [**Backlog**](project/backlog.md) - what is not built yet, by priority, with the GitHub issues.

## Guides and infrastructure

- [**Getting started**](guides/getting-started.md): run the system - in containers or on your machine -
  check it is up, seed a demo catalogue, place an order end to end, run the tests.
- [**Observability**](guides/observability.md): Seq, one trace per checkout, the queries that answer
  "what happened to order X".
- [**Troubleshooting**](guides/troubleshooting.md): the traps this project has hit, each with its
  symptom, cause and fix.
- [**Running in containers**](infrastructure/running-in-containers.md),
  [**database setup**](infrastructure/database-setup.md),
  [**RabbitMQ setup**](infrastructure/rabbitmq-setup.md).
- [**Storefront README**](../client/README.md) and the [**Bruno collection**](../bruno/).

## Concepts - study notes, not the running system

- [**`FOR UPDATE SKIP LOCKED` unit pools**](concepts/shopify-inventory-skip-locked-pattern.md): Shopify's
  flash-sale reservation design, studied and not adopted.
- [**PACELC trade-offs**](concepts/pacelc-theorem-tradeoffs.md): which parts should prefer availability
  and which consistency.

## Keeping it current

The documentation is part of every change, not a separate task:

1. **A new or changed endpoint, message, table or gateway route:** run
   `python docs/tools/generate_reference.py` from the repository root and commit the regenerated
   `docs/reference/` pages with the change. Never edit those pages by hand.
2. **A new feature or a changed rule:** update the feature's page under `features/` - its rules, data,
   API, tests and history sections - in the same pull request.
3. **Every merged feature:** add a row to the [timeline](project/timeline.md); add important decisions to
   the [decision log](project/decisions.md).
4. **A new trap found the hard way:** add it to [troubleshooting](guides/troubleshooting.md) with its
   symptom, cause and fix.
5. **An issue opened or closed:** update the [backlog](project/backlog.md).
6. **Numbers** (endpoints, tests, requests) in the [project overview](overview/project-overview.md) and the
   [testing strategy](testing/testing-strategy.md) are dated; refresh them when they move noticeably.
