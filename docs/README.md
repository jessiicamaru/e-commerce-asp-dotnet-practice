# E-Commerce Project Documentation Index

Welcome to the documentation for the E-Commerce Clean Architecture ASP.NET Core project. This directory contains detailed guides, designs, and architectural roadmaps for building the backend services.

---

## 🗺️ Documentation Map

### 🏛️ Architecture & Roadmap
* [**Roadmap & Overview**](./architecture/architecture-overview.md): High-level system modules, technology stack, and iterative development phases.
* [**Microservices Design**](./architecture/microservices-design.md): Brainstorming architecture, boundaries, and communication patterns for other modules.
* [**PACELC Theorem & Domain-Driven Trade-offs**](./architecture/pacelc-theorem-tradeoffs.md): Architectural analysis of PACELC theorem trade-offs (PA/EL for Catalog vs PC/EC for Saga & Checkout).
* [**Reliable Messaging & Outbox Pattern**](./architecture/reliable-messaging-and-outbox-pattern.md): Deep-dive guide on Transactional Outbox, Publisher/Consumer ACK protocol, RabbitMQ queue durability, and Saga Compensation.
* [**Global Cross-Cutting Error Handling & Shared Building Blocks**](./architecture/error-handling-and-shared-building-block.md): Architectural guide on RFC 7807 ProblemDetails error handling, MediatR pipeline validation, and the Ecommerce.Shared building block.
* [**Saga Orchestration & System Roadmap**](./architecture/saga-orchestration-roadmap.md): Comprehensive guide on the Saga Pattern (Orchestration vs Choreography), Standalone Saga Orchestrator Service architecture, and 5-phase master roadmap.
* [**ADR-001: Primary Key Strategy (UUID v7 vs Auto-Increment)**](./architecture/adr-001-uuidv7-primary-keys.md): Architecture Decision Record comparing UUID v4, Auto-Increment IDs, and sequential UUID v7.

### 🔌 Infrastructure & Docker
* [**Database Setup**](./infrastructure/database-setup.md): Guide on running local PostgreSQL and pgAdmin containers, and working with EF Core migrations.
* [**RabbitMQ Setup**](./infrastructure/rabbitmq-setup.md): Guide on running RabbitMQ via Docker Compose and using the Web Management Console to monitor queues.

### 🔑 Authentication Feature Module
* [**Database Schema Design**](./features/auth/db-design.md): SQL schemas, entities mapping, and data dictionary for users, roles, and refresh tokens.
* [**CQRS & MediatR Guide**](./features/auth/cqrs-guide.md): Details on command handlers, MediatR registration, and presentation mapping.
* [**JWT Middleware Configuration**](./features/auth/jwt-setup.md): Package checklist and middleware registration details to validate access tokens.
* [**Security & Token Storage Best Practices**](./features/auth/security-best-practices.md): Deep dive into XSS/CSRF token vulnerabilities and implementing the HttpOnly cookie hybrid flow.

### 🛠️ Developer Guides
* [**Troubleshooting Guide**](./guides/troubleshooting.md): Diagnosis and solutions for common C# compiler warnings, NuGet extension methods, directory mapping, and EF Core concurrency exceptions.

---

## 🚀 Quick Start (Automated One-Click Startup)

Navigate to the `server/` directory:

### Option A: PowerShell (Windows)
```powershell
./start-dev.ps1
```

### Option B: Git Bash / Linux
```bash
./start-dev.sh
```

---

## 🌐 Active Service Ports & Endpoints

| Service Name | Port | Base URL / Dashboard |
| :--- | :--- | :--- |
| **API Gateway (YARP)** | `5000` | `http://localhost:5000` |
| **Identity Service** | `5056` | `http://localhost:5056` |
| **Catalog Service** | `5057` | `http://localhost:5057` |
| **pgAdmin (DB GUI)** | `5050` | `http://localhost:5050` (`admin@admin.com` / `admin`) |
| **RabbitMQ Management** | `15672` | `http://localhost:15672` (`guest` / `guest`) |

