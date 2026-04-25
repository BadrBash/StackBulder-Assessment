# Order Processing System (ASP.NET Core, .NET 8)

## Overview

This repository is a resilient order processing backend built with **.NET 8** and **ASP.NET Core** using a **Clean Architecture** layout (Domain / Application / Infrastructure / API).

It demonstrates:
- **Concurrency-safe order placement** with inventory protection (no oversell)
- **Idempotency** via client-supplied idempotency keys
- **Outbox pattern** for reliable event publishing
- **Serializable transaction isolation** with optimistic concurrency (`RowVersion`) fallback
- **Polly retry policies** for transient fault handling
- **Background event processing** with graceful cancellation handling
- **Integration tests** covering concurrency, idempotency, validation, and CRUD scenarios

---

## Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/) (for PostgreSQL and Elasticsearch)

### Build and Run Locally

```powershell
dotnet build
dotnet run --project src/API
```

The app connects to **PostgreSQL** by default (`localhost:5432`). Ensure Postgres is running, or use Docker Compose (see below).

### Run with Docker Compose (Recommended)

This spins up the API, PostgreSQL, and Elasticsearch:

```powershell
docker-compose up --build
```

Services:
- API: `http://localhost:5000`
- PostgreSQL: `localhost:5432`
- Elasticsearch: `http://localhost:9200`

---

## API Endpoints

### Health Check

```bash
curl http://localhost:5000/health
```

### List Products

```bash
curl http://localhost:5000/api/products
```

Response:
```json
[
  { "id": "...", "name": "Widget A", "description": "Basic widget A", "price": 9.99, "stock": 100 },
  { "id": "...", "name": "Widget B", "description": "Premium widget B", "price": 19.99, "stock": 50 }
]
```

### Place an Order (with Idempotency)

```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: demo-123" \
  -d '{
    "customerEmail": "alice@example.com",
    "items": [
      { "productId": "<PRODUCT_ID>", "quantity": 1 }
    ]
  }'
```

Response (`201 Created`):
```json
{ "orderId": "..." }
```

The `Idempotency-Key` header is **optional but recommended**. Reusing the same key returns the previously created order without creating a duplicate.

### Get Order by ID

```bash
curl http://localhost:5000/api/orders/<ORDER_ID>
```

Response (`200 OK`):
```json
{
  "id": "...",
  "customerEmail": "alice@example.com",
  "total": 9.99,
  "status": "Pending",
  "createdAt": "2024-01-15T10:30:00Z",
  "items": [
    { "productId": "...", "productName": "Widget A", "quantity": 1, "unitPrice": 9.99 }
  ]
}
```

Returns `404 Not Found` if the order does not exist.

---

## Input Validation

The API validates incoming requests:
- `customerEmail` must be a valid email address (contains `@`)
- `items` array must contain at least one item
- Each item's `quantity` must be greater than 0

Invalid requests return `400 Bad Request` with a descriptive error message.

---

## Testing

Integration tests use `WebApplicationFactory` and run against a real PostgreSQL database. Run tests:

```bash
dotnet test tests/Integration
```

Test coverage includes:
- **Concurrency test**: 10 simultaneous orders for the same product — ensures no overselling
- **Idempotency test**: Duplicate requests with the same key return the same order
- **Validation test**: Empty email, missing items, and negative quantities return `400`
- **GET order test**: Successfully retrieves a placed order by ID
- **404 test**: Returns `404` for non-existent orders

---

## Core Concepts & Architecture

### Clean Architecture Separation
- `src/Domain` — Entities and domain models (`Order`, `OrderItem`, `Product`)
- `src/Application` — DTOs and service interfaces (no infrastructure dependencies)
- `src/Infrastructure` — EF Core, repositories, eventing, outbox, background workers
- `src/API` — ASP.NET Core minimal API wiring, DI, and seeding

### Outbox Pattern
Events are stored in the `OutboxMessages` table **within the same database transaction** as the order. A background `OutboxPublisher` reliably publishes them to the configured event bus. This guarantees **at-least-once delivery** without dual-write problems.

### Eventing
An `InMemoryEventBus` is used for in-process demo handling. It is **thread-safe** (uses locking) for concurrent access. For multi-instance systems, replace this with a broker-backed implementation (RabbitMQ, NATS, Azure Service Bus).

### Concurrency Control
- **Serializable isolation** is set on PostgreSQL transactions for order placement
- **Pessimistic locking** via `SELECT ... FOR UPDATE` on product rows
- **Optimistic concurrency** via `RowVersion` tokens as a fallback
- **Polly retry policy** handles transient `DbUpdateConcurrencyException`s

### Idempotency
The idempotency check is performed **inside the serializable transaction**, preventing race conditions where two concurrent requests with the same key could create duplicate orders.

---

## Assumptions

- Demo focuses on core ordering guarantees (no authentication, no external payment gateway integration).
- Client supplies an `Idempotency-Key` for idempotent order requests.
- Single-database setup for the demo (Outbox shares the same DB). For higher durability, consider separate durable stores or brokers.

---

## Trade-offs

- **Simplicity vs production readiness**: In-memory event bus is simple to run locally but is not suitable for production-scale, multi-instance deployments.
- **Outbox**: Persisting to the same DB reduces lost-event risk but still couples publishing durability to the DB; a broker-based architecture is recommended for production.
- **Concurrency**: Serializable isolation and `RowVersion` are safe but may increase contention; tune according to workload.

---

## Extending for Production

- [ ] Replace `InMemoryEventBus` with a broker-backed implementation (RabbitMQ/NATS) and have `OutboxPublisher` forward to that broker
- [ ] Add EF Core migrations instead of `EnsureCreated()`
- [ ] Harden APIs with authentication, authorization, and rate-limiting
- [ ] Add OpenTelemetry tracing, metrics, and structured logging
- [ ] Add a CI/CD pipeline (GitHub Actions)
- [ ] Use Testcontainers for integration test database isolation

---

## Files to Inspect

| Layer | File | Purpose |
|-------|------|---------|
| API | [`src/API/Program.cs`](src/API/Program.cs) | App wiring, endpoints, DI, seeding |
| API | [`src/API/appsettings.json`](src/API/appsettings.json) | Connection strings and logging config |
| Domain | [`src/Domain/Entities/Order.cs`](src/Domain/Entities/Order.cs) | Order aggregate root |
| Domain | [`src/Domain/Entities/Product.cs`](src/Domain/Entities/Product.cs) | Product with concurrency token |
| Application | [`src/Application/DTOs/OrderRequest.cs`](src/Application/DTOs/OrderRequest.cs) | Order request DTOs |
| Application | [`src/Application/Interfaces/IOrderService.cs`](src/Application/Interfaces/IOrderService.cs) | Order service contract |
| Infrastructure | [`src/Infrastructure/Services/OrderService.cs`](src/Infrastructure/Services/OrderService.cs) | Core order placement logic |
| Infrastructure | [`src/Infrastructure/OrderDbContext.cs`](src/Infrastructure/OrderDbContext.cs) | EF Core context |
| Infrastructure | [`src/Infrastructure/Outbox/OutboxMessage.cs`](src/Infrastructure/Outbox/OutboxMessage.cs) | Outbox message entity |
| Infrastructure | [`src/Infrastructure/Outbox/OutboxPublisher.cs`](src/Infrastructure/Outbox/OutboxPublisher.cs) | Background outbox publisher |
| Infrastructure | [`src/Infrastructure/Eventing/InMemoryEventBus.cs`](src/Infrastructure/Eventing/InMemoryEventBus.cs) | Thread-safe in-memory event bus |
| Infrastructure | [`src/Infrastructure/HostedServices/OrderBackgroundProcessor.cs`](src/Infrastructure/HostedServices/OrderBackgroundProcessor.cs) | Background order processor |
| Tests | [`tests/Integration/OrderConcurrencyTests.cs`](tests/Integration/OrderConcurrencyTests.cs) | Integration tests |

---

## Tech Stack

- **.NET 8** / ASP.NET Core Minimal APIs
- **EF Core 8** with Npgsql (PostgreSQL provider)
- **Polly** for resilience
- **Serilog** with Elasticsearch sink
- **xUnit** + `WebApplicationFactory` for integration testing
- **Docker & Docker Compose**

---

*Updated: 2025*

