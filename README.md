Order Processing System (ASP.NET Core, .NET 8)
============================================

Overview
--
This sample implements a resilient order processing backend following Clean Architecture.

Why `Application/Services` and `Application/Events` may be empty
--
- I intentionally kept application-layer minimal: interfaces and DTOs live under `src/Application`.
- Concrete implementations (like `OrderService`) that require infrastructure (EF Core, Npgsql, Polly) are placed in `src/Infrastructure` to avoid project circular references and to keep the Application project pure (no external deps).

Features added
--
- Postgres LISTEN/NOTIFY eventing (`PostgresEventBus` + listener hosted service)
- Retry policies using Polly for transient DB/concurrency errors
- Structured logging via Serilog (console + Elasticsearch sink)
- Dockerfile and `docker-compose.yml` (Postgres + Elasticsearch + API)
- Integration test scaffold (requires Postgres running via docker-compose)

Run locally (docker)
--
1. Start services:

```bash
docker-compose up -d --build
```

2. The API will be available at http://localhost:5000

Run tests
--
- The integration tests assume Postgres is available at `localhost:5432`.
- Start docker-compose first, then run:

```bash
dotnet test tests/Integration
```

Notes
--
- The integration test `OrderConcurrencyTests` contains a placeholder `REPLACE_WITH_PRODUCT_ID` — replace it with a real product id from the database, or extend the test to query the product id programmatically.
- For production-grade event delivery, add an outbox pattern or durable broker (RabbitMQ/Kafka) to avoid message loss on failures between commit and external publish. This example demonstrates pg_notify as a lightweight approach.
