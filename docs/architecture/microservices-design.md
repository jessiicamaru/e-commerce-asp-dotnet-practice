# E-Commerce Microservices Architecture Design

This document details the microservices architecture proposal for the other modules of the E-commerce platform. It outlines service boundaries, database design principles, and service communication patterns (gRPC vs. RabbitMQ).

---

## 1. System Topology (Mermaid)

```mermaid
graph TD
    Client[Web/Mobile Client] -->|HTTPS| Gateway[API Gateway - YARP]
    
    %% Services
    Gateway -->|gRPC/REST| AuthService[Auth & Identity Service]
    Gateway -->|REST| CatalogService[Product Catalog Service]
    Gateway -->|REST| OrderService[Ordering Service]
    
    %% Communication & Broker
    OrderService -->|gRPC Check Stock| InventoryService[Inventory Service]
    OrderService -->|Publish Events| RabbitMQ[RabbitMQ Broker]
    
    RabbitMQ -->|Consume events| NotificationService[Notification Service]
    RabbitMQ -->|Consume events| InventoryService
    RabbitMQ -->|Consume events| PaymentService[Payment Service]
    
    %% Databases
    AuthService --> DB1[(Auth DB - PostgreSQL)]
    CatalogService --> DB2[(Catalog DB - MongoDB/PostgreSQL)]
    OrderService --> DB3[(Order DB - PostgreSQL)]
    InventoryService --> DB4[(Inventory DB - Redis/PostgreSQL)]
    PaymentService --> DB5[(Payment DB - PostgreSQL)]
```

---

## 2. Microservice Module Breakdown

Each microservice is fully self-contained, owning its business logic, database, and scaling profile.

### 2.1 Identity & Auth Service (Present)
* **Responsibility**: User management, authentication, role assignment, token validation (Access + Refresh tokens).
* **Database**: `ecommerce-identity-db` (Postgres).
* **Key Entities**: `User`, `Role`, `RefreshToken`.

### 2.2 Product Catalog Service
* **Responsibility**: Managing brands, categories, dynamic product specifications, pricing, search indexes, and media.
* **Database**: `ecommerce-catalog-db` (Document database like **MongoDB** is recommended due to the schema-less, polymorphic nature of dynamic product attributes; otherwise Postgres with JSONB columns).
* **Key Entities**: `Product`, `Category`, `Brand`, `ProductAttribute`.
* **Caching**: Highly read-heavy. Uses Redis cache to serve catalog endpoints under 10ms.

#### 2.3 Ordering Service (Implemented: `Ecommerce.Order`)
* **Responsibility**: Shopping cart persistence, checkout validation, price calculations, order creation, and publishing `OrderSubmittedEvent`.
* **Database**: `ecommerce-order-db` (Port `5434`, Postgres, due to strong transactional ACID requirements).
* **Key Entities**: `Order`, `OrderItem`.

### 2.4 Saga Orchestrator Service (Implemented: `Ecommerce.Orchestrator`)
* **Responsibility**: Standalone microservice running MassTransit `OrderStateMachine` Saga to coordinate multi-service distributed transactions (Inventory reservation, Payment processing, and Compensating Transactions).
* **Database**: `ecommerce-orchestrator-db` (Port `5436`, Postgres, storing `order_state_data` Saga state).
* **Key Entities**: `OrderStateData`.

### 2.5 Inventory Service
* **Responsibility**: Real-time stock counting, reserving items during checkout, updating stock levels on new shipments, and resolving low stock alerts.
* **Database**: `ecommerce-inventory-db` (Postgres or Redis for ultra-fast distributed locking of inventory to prevent double-selling).
* **Key Entities**: `Inventory`, `StockReservation`, `StockMovement`.

### 2.6 Payment Service
* **Responsibility**: Initiating transactions, interfacing with Stripe, PayPal, VNPay API, and processing payment gateway webhooks securely.
* **Database**: `ecommerce-payment-db` (Postgres, audit trail of transactions).
* **Key Entities**: `PaymentTransaction`, `Refund`.

### 2.7 Notification Service
* **Responsibility**: Decoupled worker service that listens to events and dispatches customer emails (registration confirmation, invoice PDF, order shipment tracking).
* **Database**: None or tiny audit database.
* **Communication**: 100% Event-driven (never calls other services synchronously).

---

## 3. Communication Patterns

### 3.1 Asynchronous Event-Driven & Saga Orchestration (Broker: RabbitMQ)
Used when a service needs to trigger actions in other services without waiting for a response, ensuring high resilience, eventual consistency, and compensation rollback.

```text
Ordering Service (Submit Order)
      │
      ▼ (OrderSubmittedEvent via Transactional Outbox)
RabbitMQ Broker ──► Saga Orchestrator (OrderStateMachine)
                          │
         ┌────────────────┴────────────────┐
         ▼                                 ▼
[ReserveInventoryCommand]        [ProcessPaymentCommand]
         │                                 │
         ▼                                 ▼
[Catalog/Inventory Service]      [Payment Service]
 (Reserve Bounded Stock)          (Process Charge)
         │                                 │
         └─────────────┬───────────────────┘
                       ▼ (Failure Compensation)
         [ReleaseInventoryCommand] ──► Rollback Inventory
```

### 3.2 Synchronous RPC (Protocol: gRPC)
Used for fast, low-latency, strongly-typed internal service-to-service queries where a real-time response is required before proceeding.

* **Example**: During Checkout, the `Ordering Service` must query the `Inventory Service` to confirm if products are still in stock before creating the order record.
* **Protocol**: **gRPC (HTTP/2 Protocol Buffers)** instead of standard REST. gRPC is up to **8x faster** and consumes significantly less network bandwidth due to binary serialization.

---

## 4. API Gateway Configuration (YARP)

We use Microsoft's official **YARP (Yet Another Reverse Proxy)** library running in [`Ecommerce.ApiGateway`](file:///d:/Code/CSharp/e-commerce/server/src/ApiGateway/Ecommerce.ApiGateway/) on **Port `5000`**.

### Route Mappings in `appsettings.json`

```json
  "ReverseProxy": {
    "Routes": {
      "identity-route": {
        "ClusterId": "identity-cluster",
        "Match": { "Path": "/api/auth/{**catch-all}" }
      },
      "catalog-categories-route": {
        "ClusterId": "catalog-cluster",
        "Match": { "Path": "/api/categories/{**catch-all}" }
      },
      "catalog-products-route": {
        "ClusterId": "catalog-cluster",
        "Match": { "Path": "/api/products/{**catch-all}" }
      },
      "order-route": {
        "ClusterId": "order-cluster",
        "Match": { "Path": "/api/orders/{**catch-all}" }
      }
    },
    "Clusters": {
      "identity-cluster": {
        "Destinations": { "destination1": { "Address": "http://localhost:5056/" } }
      },
      "catalog-cluster": {
        "Destinations": { "destination1": { "Address": "http://localhost:5057/" } }
      },
      "order-cluster": {
        "Destinations": { "destination1": { "Address": "http://localhost:5059/" } }
      }
    }
  }
```

---

## 5. Key Challenges & Patterns to Implement

When transitioning to Microservices, you will need to implement:
1. **Saga Pattern (Orchestration/Choreography)**: To manage distributed transactions across multiple databases (e.g. if payment fails, roll back inventory reservation).
2. **API Gateway (YARP - Yet Another Reverse Proxy)**: A single .NET gateway proxying requests from the browser to the individual microservices on port `5000`.
3. **Outbox Pattern**: To guarantee that database updates and RabbitMQ event publishing happen atomically in a single transaction.
