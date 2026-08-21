# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A hands-on microservices lab (.NET 10, Minimal APIs) built incrementally to practice service design, inter-service
communication, event-driven architecture, and orchestration patterns. Domain: a small e-commerce flow (Catalog →
Basket → Orders). Each service, and every pattern applied to it, was introduced as a deliberate teaching step — see
"Architecture" below for what each module demonstrates.

## Commands

### Build / run without Docker
```bash
dotnet build MicroservicesLab.slnx              # build everything
dotnet build services/catalog/Catalog.Api       # build a single project
dotnet run --project services/catalog/Catalog.Api
```
There are no automated tests in this repo yet.

### Docker Compose (the primary way to run the full stack)
```bash
docker compose up -d --build     # build images and start everything
docker compose ps                # status of all containers
docker compose logs -f <service> # e.g. basket-api, catalog-api, orders-api, kafka
docker compose down               # stop everything (keeps DB volumes)
docker compose down -v            # also wipes Postgres volumes — needed after changing an EF Core model,
                                   # since services use `EnsureCreated()` rather than migrations
```
Service names: `catalog-db`, `orders-db`, `redis`, `kafka`, `kafka-ui`, `catalog-api`, `basket-api`, `orders-api`,
`api-gateway`. `compose.debug.yaml` is stale VS-generated scaffolding — not part of the real workflow, safe to ignore.

### Hybrid debugging (breakpoints without rebuilding containers)
`.vscode/launch.json` has a `coreclr` config per service (`Catalog.Api`, `Basket.Api`, `Orders.Api`, `ApiGateway`)
plus a compound "Todos los servicios (local)". Workflow: leave dependencies running in Docker, stop the *container*
for the one service you're debugging (`docker compose stop basket-api`), then F5 that config in VS Code — its
`appsettings.json` already points at `localhost` ports for exactly this case. Postgres ports are published to the
host (`5432` catalog, `5433` orders) specifically so a locally-run service can reach its dockerized database.
Caveat: stopping a container that *other* dockerized services depend on breaks their internal DNS lookup
(`http://catalog-api:8080`) — to debug a call between two services, run both locally instead of just one.

### Manual endpoint testing
Each service has a `.http` file (`Catalog.Api.http`, `Basket.Api.http`, `Orders.Api.http`) with working example
requests — usable with the REST Client VS Code extension. Kafka messages are best inspected via Kafka UI at
`http://localhost:8090` rather than logs.

## Architecture

### Services and why they're split this way
- **`services/catalog/Catalog.Api`** (port 5093) — product catalog. Postgres (`catalog-db`), owns product data and
  stock.
- **`services/basket/Basket.Api`** (port 5005) — shopping cart. Redis via `IDistributedCache`, not Postgres —
  deliberately a different storage technology per service (polyglot persistence), chosen because cart data is
  ephemeral/session-scoped rather than a durable record.
- **`services/orders/Orders.Api`** (port 5139) — checkout/orders. Postgres (`orders-db`).
- **`gateway/ApiGateway`** (port 8080) — YARP reverse proxy, the single external entry point. Routes `/products/**`
  → catalog, `/basket/**` → basket, `/orders/**` → orders (config in its `appsettings.json` under `ReverseProxy`).
  **Service-to-service calls do NOT go through the gateway** — Basket→Catalog and Orders→Basket call each other's
  container directly (`http://catalog-api:8080`, resolved via Docker's internal DNS). The gateway is for north-south
  traffic only; routing internal calls through it would add latency and a needless dependency.
- **`shared/Shared.Contracts`** — the *only* thing shared between services. Contains:
  - `Events/` — Kafka event payload records (`OrderCreatedEvent`, `StockReservedEvent`,
    `StockReservationFailedEvent`) and `KafkaTopics` topic-name constants. Sharing these is intentional: an event's
    schema is the public contract between producer and consumers, so duplicating it risks silent deserialization
    drift.
  - `Messaging/` — `IEventPublisher`/`KafkaEventPublisher`, a thin Kafka producer wrapper. Shared because it's
    generic infrastructure plumbing, not business logic.
  - **Domain models are never shared** across services (e.g. Basket has its own `CatalogProductDto`, Orders has its
    own `BasketDto`) even though they overlap with another service's real model — each service owns its own view of
    data it doesn't own, so services stay independently deployable.

### Inter-service communication
- **Synchronous (HTTP)**: typed `HttpClient`s (`AddHttpClient<TInterface, TImpl>`) — `Basket.Api.Services.
  CatalogServiceClient` (Basket → Catalog) and `Orders.Api.Services.BasketServiceClient` (Orders → Basket). Both are
  wrapped with `.AddStandardResilienceHandler(...)` (Polly v8 via `Microsoft.Extensions.Http.Resilience`): retries,
  circuit breaker, timeouts. Endpoints that call through these clients catch `HttpRequestException` /
  `BrokenCircuitException` / `TimeoutRejectedException` explicitly and return a `503 ProblemDetails` rather than
  letting a raw 500 leak out.
- **Asynchronous (Kafka)**: each consumer is a `BackgroundService` (`Messaging/` folder in each service) running its
  own consumer loop on a dedicated thread (`Task.Run`, since `Confluent.Kafka`'s `Consume()` is blocking). Consumer
  loops catch `ConsumeException` per-iteration and retry with a short delay — letting it bubble up would trigger
  ASP.NET Core's default `BackgroundServiceExceptionBehavior.StopHost` and kill the entire web host over a
  transient Kafka hiccup (e.g. topic not yet created). Kafka runs in KRaft mode (no Zookeeper), single broker,
  `apache/kafka` image.

### The checkout saga (choreography, not orchestration)
`POST /orders` does *not* synchronously reserve stock. It reads the basket from Basket.Api over HTTP, persists an
`Order` with `Status = Pending`, and publishes `OrderCreatedEvent`. From there, each service reacts independently:

1. `Catalog.Api`'s `OrderCreatedConsumer` checks stock for **all** items first, before mutating anything
   (all-or-nothing). If everything's available, it decrements stock and publishes `StockReservedEvent`; otherwise it
   publishes `StockReservationFailedEvent` with a reason and touches nothing.
2. `Orders.Api` has two more consumers (`StockReservedConsumer`, `StockReservationFailedConsumer`) that flip the
   order's `Status` to `Confirmed` or `Cancelled`.
3. `Basket.Api`'s consumer listens for `StockReservedEvent` (not `OrderCreatedEvent`) and only *then* clears the
   cart. This is why there's no separate "restore the cart" compensation path: the cart is never touched until the
   order is known to have succeeded, so a `StockReservationFailedEvent` requires no rollback in Basket.

When changing this flow, keep the ordering intent: nothing should mutate durable/user-visible state on the *request*
event — only on its confirmation.

### Data model conventions
- Entities are C# `record`s. EF Core can bind scalar constructor parameters directly, but **cannot** bind owned
  collection navigations through a positional constructor — `Order.Items` (an EF `OwnsMany`) is therefore a
  post-constructor `init` property, not a constructor parameter. Follow this pattern for any new owned collection.
- Mutating a tracked record entity goes through `db.Entry(entity).CurrentValues.SetValues(entity with { ... })`
  rather than re-fetching or hand-assigning fields — see `Catalog.Api`'s stock decrement and `Orders.Api`'s status
  updates.
- No EF Core migrations — `Database.EnsureCreated()` runs on startup in `Development`. Schema changes require
  `docker compose down -v` to drop and recreate the Postgres volumes.
- Catalog seeds itself with sample products on first run (`CatalogDbSeeder`, `Development`-only).

### Adding a new service
Mirror an existing one: `Dockerfile` copies `services/<area>/<Name>/*.csproj` **and**
`shared/Shared.Contracts/Shared.Contracts.csproj` before `dotnet restore` (both are needed since the build context
is the repo root, not the project folder). Register it in `MicroservicesLab.slnx`, add it to `compose.yaml` with an
`ASPNETCORE_HTTP_PORTS: 8080` env var (the base image's default differs), and add a route/cluster in
`gateway/ApiGateway/appsettings.json` if it should be externally reachable.
