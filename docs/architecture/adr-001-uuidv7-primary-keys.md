# ADR 001: Primary Key Strategy for Microservices (UUID v7 vs. Auto-Increment)

* **Status**: Accepted
* **Deciders**: Architecture Team
* **Date**: 2026-09-02

---

## 1. Context & Problem Statement

In a distributed Monorepo Microservices architecture, selecting an appropriate primary key (PK) strategy for database entities is a fundamental design decision. 

We need an identifier strategy that:
1. Ensures **global uniqueness** across isolated database instances (`ecommerce_identity_db`, `ecommerce_catalog_db`, etc.).
2. Prevents ID collision when publishing domain events over **RabbitMQ**.
3. Minimizes **B-Tree index fragmentation** and disk page splits in PostgreSQL.
4. Protects public API endpoints against **ID enumeration attacks** (e.g., `/api/products/1`, `/api/products/2`).

---

## 2. Option Comparison Matrix

| Criteria | Auto-Increment (`bigint`) | Random UUID v4 (`Guid.NewGuid()`) | **Sequential UUID v7 (`Guid.CreateVersion7()`)** |
| :--- | :--- | :--- | :--- |
| **Global Uniqueness** | ❌ Local to 1 DB table only | ✅ Globally Unique | ✅ **Globally Unique** |
| **Client-side Generation** | ❌ Must wait for DB `INSERT` | ✅ Can generate in C# | ✅ **Can generate in C#** |
| **API Endpoint Security** | ❌ Vulnerable to enumeration | ✅ Unpredictable | ✅ **Unpredictable** |
| **Storage Size** | 8 bytes | 16 bytes | 16 bytes |
| **PostgreSQL B-Tree Indexing** | ✅ Sequential (No Page Splits) | ❌ High Fragmentation (Random Page Splits) | ✅ **Sequential (No Page Splits)** |
| **Time-ordered Sorting** | ✅ Monotonic | ❌ Random | ✅ **Monotonic (Built-in Timestamp)** |

---

## 3. Decision & Rationale

We choose **Sequential UUID v7 (`Guid.CreateVersion7()`)** as the standard Primary Key data type across all microservice domain entities.

### Rationale

1. **Eliminating B-Tree Index Fragmentation**:
   - Standard random UUID v4 inserts nodes at random locations across the PostgreSQL B-Tree index, causing severe **page splits** and degrading `INSERT` performance as table size grows.
   - UUID v7 encodes a **48-bit Unix timestamp** at the beginning of the bit array. As a result, newly inserted keys are always appended sequentially to the rightmost leaf of the B-Tree index, achieving `INSERT` performance comparable to auto-incrementing integers.

2. **Distributed System Safety**:
   - Microservices can safely generate entity IDs on RAM before executing database transactions, simplifying **Saga Pattern orchestrations** and **Event-Driven messaging** via RabbitMQ.

3. **Built-in Security**:
   - Guids exposed in REST API URLs (e.g., `/api/products/019183ab-4f21-7d12-...`) prevent competitors or malicious actors from scanning endpoints or estimating total order volumes.

---

## 4. Code Implementation Guidelines (.NET 10)

### 4.1 Domain Entity Definition
```csharp
namespace Ecommerce.Catalog.Domain.Entities;

public class Product
{
    public Guid Id { get; set; } // Uses Guid (UUID v7 format)
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

### 4.2 Application Handler Generation
When explicitly instantiating new domain entities in C# application handlers:

```csharp
// Use Guid.CreateVersion7() in .NET 9/10 for time-ordered sequential GUIDs
var categoryId = Guid.CreateVersion7();
```

---

## 5. Summary

By standardizing on **UUID v7**, our backend gains the distributed architecture advantages of UUIDs (global uniqueness, client-side generation, security) while eliminating traditional PostgreSQL indexing degradation.
