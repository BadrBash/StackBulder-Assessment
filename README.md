# Order Processing System (ASP.NET Core, .NET 8)

Overview
--
This repository is a demo of a resilient order processing backend built with .NET 8 and ASP.NET Core using a Clean Architecture layout (Domain / Application / Infrastructure / API).
It demonstrates concurrency-safe order placement, inventory protection (no oversell), idempotency, an Outbox pattern for reliable event publishing, and integration tests.

Quick Start
--
Prerequisites
- .NET 8 SDK
- Docker (optional, for container runs)
- Optional: `sqlite3` to inspect the generated SQLite DB

Build and run locally

```powershell
dotnet build
dotnet run --project src/API
```

The app creates a local SQLite file (`orders.db`) and seeds sample products on startup. For development and tests the app deletes any existing `orders.db` file at startup to avoid schema drift — remove that logic before using a persistent DB in production.

Health check

```bash
curl http://localhost:5000/health
```

List products

```bash
curl http://localhost:5000/api/products
# or, inspect DB directly (if you have sqlite3):
sqlite3 orders.db "SELECT Id, Name, Description, Stock, Price FROM Products;"
```

Place an order (Idempotency)

```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: demo-123" \
  -d '{"customerEmail":"alice@example.com","items":[{"productId":"<PRODUCT_ID>","quantity":1}]}'
```

The `Idempotency-Key` header is optional but recommended: if the same key is reused the service returns the previously created order instead of creating a duplicate.

Running with Docker
--
A `Dockerfile` and a simple `docker-compose.yml` are included for quick runs. To build and run the API container:

```powershell
docker-compose up --build
```

Testing
--
Integration tests use `WebApplicationFactory` and SQLite. Run tests:

```bash
dotnet test tests/Integration
```

Core Concepts & Architecture
--
- Clean Architecture separation:
  - `src/Domain` — entities and domain models
  - `src/Application` — DTOs and interfaces (no infra dependencies)
  - `src/Infrastructure` — EF Core, repositories, eventing, outbox, background workers
  - `src/API` — ASP.NET Core minimal API wiring, DI, and seeding
- Outbox pattern: persisted events are stored in the `OutboxMessages` table and a background `OutboxPublisher` reliably publishes them to the configured event bus.
- Eventing: an `InMemoryEventBus` is used for in-process demo handling. For multi-instance systems replace this with a broker-backed implementation.
- Concurrency: the `OrderService` attempts to set serializable isolation where supported and uses an optimistic concurrency `RowVersion` token as a fallback. Polly is used for transient retries.

Assumptions
--
- Demo focuses on core ordering guarantees (no authentication, no external payment gateway integration).
- Client supplies an `Idempotency-Key` for idempotent order requests.
- Single-database setup for the demo (Outbox shares the same DB). For higher durability, consider separate durable stores or brokers.

Trade-offs
--
- Simplicity vs production readiness: SQLite + in-memory event bus are simple to run locally but are not suitable for production-scale, multi-instance deployments.
- Outbox: persisting to the same DB reduces lost-event risk but still couples publishing durability to the DB; a broker-based architecture and separate durable store are recommended for production.
- Concurrency: attempting serializable isolation and/or `RowVersion` is safe but may increase contention; tune according to workload.

Extending for production
--
- Use Postgres (or another production RDBMS) and EF Core migrations instead of `EnsureCreated()` and file-based SQLite.
- Replace `InMemoryEventBus` with a broker-backed implementation and have `OutboxPublisher` forward to that broker.
- Harden APIs (auth, validation, rate-limiting) and add monitoring/tracing (OpenTelemetry), metrics, and centralized logging.

Files to inspect
--
- App wiring: [src/API/Program.cs](src/API/Program.cs)
- Order logic: [src/Infrastructure/Services/OrderService.cs](src/Infrastructure/Services/OrderService.cs)
- EF Core context: [src/Infrastructure/OrderDbContext.cs](src/Infrastructure/OrderDbContext.cs)
- Outbox: [src/Infrastructure/Outbox/OutboxMessage.cs](src/Infrastructure/Outbox/OutboxMessage.cs), [src/Infrastructure/Outbox/OutboxRepository.cs](src/Infrastructure/Outbox/OutboxRepository.cs), [src/Infrastructure/Outbox/OutboxPublisher.cs](src/Infrastructure/Outbox/OutboxPublisher.cs)
- Event bus: [src/Infrastructure/Eventing/InMemoryEventBus.cs](src/Infrastructure/Eventing/InMemoryEventBus.cs)
- Background processors: [src/Infrastructure/HostedServices/OrderBackgroundProcessor.cs](src/Infrastructure/HostedServices/OrderBackgroundProcessor.cs)
- Integration tests: [tests/Integration](tests/Integration)

Notes and cautions
--
- The app deletes `orders.db` during startup in the API for development convenience. Remove that behavior for persistent data in production and use proper migrations.
- For multiple instances, use a message broker and durable outbox to enable reliable cross-instance delivery.

Next steps (suggested)
--
- Add broker integration (RabbitMQ or NATS) and wire the OutboxPublisher to publish to it.
- Harden integration tests using Testcontainers and add a CI workflow.
- Switch the project to EF Core migrations and remove the development DB reset logic.

---
Updated: April 2026
