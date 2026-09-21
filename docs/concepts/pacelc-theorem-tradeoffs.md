# Architecture Guide: PACELC Theorem & Domain-Driven Trade-offs

> **Study note.** This applies PACELC to the system as it was *intended*. The Redis caching, read
> replicas and sub-50ms targets mentioned below were never built, and one classification has changed
> since — see [§4](#4-where-the-running-system-departs-from-this-note).

This document describes the application of the **PACELC Theorem** to analyze system trade-offs between **Availability (A)**, **Latency (L)**, and **Consistency (C)** across our Monorepo Microservices.

---

## 1. PACELC Theorem Overview

Proposed by Daniel Abadi in 2012, the **PACELC Theorem** extends the CAP theorem by evaluating system trade-offs not only during network partitions (**P**), but also during normal execution (**E**lse):

$$\text{If } \mathbf{P} \text{ (Partition): } [\mathbf{A}\text{vailability} \quad \text{vs} \quad \mathbf{C}\text{onsistency}]$$
$$\text{Else } (\mathbf{E}): \quad [\mathbf{L}\text{atency} \quad \text{vs} \quad \mathbf{C}\text{onsistency}]$$

---

## 2. Domain-Driven PACELC Trade-off Strategy

Rather than enforcing a uniform PACELC configuration across all microservices, we partition our system into **Domain-Driven Subsystems** with tailored consistency vs. availability models:

```text
               ┌─────────────────────────────────────────────────────────┐
               │           PACELC TRADE-OFF ARCHITECTURE                 │
               └────────────────────────────┬────────────────────────────┘
                                            │
               ┌────────────────────────────┴────────────────────────────┐
               ▼                                                         ▼
  🛒 Catalog & Product Browsing                          💳 Order, Inventory & Payment
  (PA / EL Subsystem)                                    (PC / EC Subsystem)
  - Partition (P): Priority = Availability (A)          - Partition (P): Priority = Consistency (C)
  - Else (E): Priority = Low Latency (L)                 - Else (E): Priority = Strict Consistency (C)
  - Consistency: Eventual Consistency                    - Consistency: Strong Consistency
```

---

### 2.1 Catalog & Browsing Subsystem (`Ecommerce.Catalog`) $\rightarrow$ **PA / EL**

* **Partition Scenario (P $\rightarrow$ A)**:
  If a network partition occurs between database replicas or services, `Ecommerce.Catalog` prioritizes **Availability (A)**. Customers must always be able to browse product listings and prices, even if reading slightly stale cached data.
* **Normal Scenario (E $\rightarrow$ L)**:
  Under normal operation, the subsystem prioritizes **Low Latency (L)** using Redis caching and read replicas. Changes made by admins (e.g., price updates) embrace **Eventual Consistency**, propagating to read caches asynchronously.

---

### 2.2 Order, Stock & Payment Subsystem (`Ecommerce.Order`, `Ecommerce.Inventory`, `Ecommerce.Orchestrator`) $\rightarrow$ **PC / EC**

* **Partition Scenario (P $\rightarrow$ C)**:
  If a network partition isolates `Inventory Service` or `Payment Service`, the system prioritizes **Strong Consistency (C)**. New checkout transactions are rejected (sacrificing Availability) to prevent catastrophic business errors such as **over-selling stock**.
* **Normal Scenario (E $\rightarrow$ C)**:
  Under normal operation, the subsystem prioritizes **Consistency (C)**. The system accepts slight latency overheads (100ms - 300ms) to execute **Transactional Outbox writes** and **Saga State Machine transitions** before confirming order success.

---

## 3. System Component PACELC Mapping Matrix

| Microservice / Component | PACELC Classification | Architectural Trade-off |
| :--- | :--- | :--- |
| **Catalog Read APIs** | **PA / EL** | Optimized for 24/7 uptime and sub-50ms HTTP responses using Redis caching. |
| **Transactional Outbox & RabbitMQ** | **PC / EC** | Enforces *at-least-once delivery* over raw throughput. It does **not** order messages of different types — consumers must tolerate any order (issue #15). |
| **Saga Orchestrator Service** | **PC / EC** | Persists state transitions to `ecommerce_saga_db` to guarantee financial & stock integrity. |
| **Ordering & Inventory Services** | **PC / EC** | Blocks illegal inventory deductions, prioritizing correctness over raw latency. |

---

## 4. Where the Running System Departs from This Note

- **Catalog is no longer purely PA.** Since [feature 009](../../specs/009-catalog-owns-price/),
  checkout asks Catalog for prices synchronously. If Catalog is unreachable, checkout is **refused
  with 503** rather than priced from anything stale or anything the client claimed — pricing chose
  consistency over availability. Browsing remains best-effort.
- **Cart chooses availability for display, consistency for checkout.** When Catalog does not answer,
  the cart still renders its lines with prices marked unavailable, and checkout is disabled.
- **No cache, no replicas.** Every read goes to the service's own PostgreSQL. The latency figures
  above are targets from the original design, never measured.
