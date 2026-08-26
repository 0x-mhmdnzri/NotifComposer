# Backend .NET Code Challenge – Employee Management Microservices

Microservice system with **SOLID**, **Transactional Outbox/Inbox**, and **latency engineering** practices.

## Architecture

```
┌─────────────────┐   gRPC + deadline (150ms)   ┌─────────────────┐
│  Employee       │ ──────────────────────────► │  Identity       │
│  Service        │                             │  Service        │
└────────┬────────┘                             └─────────────────┘
         │ Outbox (same DB txn) — NOT on critical path of caller
         ▼
    ┌─────────┐
    │  Kafka  │
    └────┬────┘
         │ Inbox (exactly-once)
         ▼
┌─────────────────┐
│  Notification   │
│  Service        │
└─────────────────┘
```

## Latency design (software-latency-engineering)

Latency is treated as a **measurable delay between two points**, with a **distribution** (not a single average).

### Latency budget (targets)

| Path | p50 target | p99 / slow threshold | Dominant stage |
|------|------------|----------------------|----------------|
| Identity GET by id | &lt; 20ms | 200ms | DB lookup |
| Identity list | &lt; 40ms | 200ms | DB + serialization |
| Employee create | &lt; 80ms | 400ms | gRPC UserExists + DB write |
| Employee GET | &lt; 25ms | 200ms | DB (or cache hit) |
| Notification GET | &lt; 25ms | 200ms | DB (or cache hit) |

### Measure

- `LatencyMiddleware` on every service: logs duration + status; warns on requests above budget.
- Response header `X-Response-Time-Ms` so load generators can collect percentiles without coordinated omission tricks.
- Structured logs → aggregate to p50/p95/p99 in your log backend.

### Reduce

| Technique | Where |
|-----------|--------|
| Connection pooling + bounded CommandTimeout (5s) | All Npgsql connection strings |
| Shared gRPC channel + HTTP/2 keep-alive | `IdentityGrpcClient` |
| Indexes on lookup keys (UserId, Mobile, …) | EF model |
| `AsNoTracking` on reads | Repositories |
| Response compression (Brotli/Gzip, fastest level) | All HTTP APIs |
| Fetch only needed rows (pagination max 100) | List endpoints |

### Hide

| Technique | Where | Staleness policy |
|-----------|--------|------------------|
| **Output cache** GET by id (30s) / list (10s) | Controllers | Evict by tag on every write |
| **Outbox** for notification | Employee create | Caller does not wait for Notification/Kafka |
| **gRPC deadline 150ms** | Identity check | Bounds dependency contribution to Employee create p99 |

Notification delivery is **async** (eventual consistency). Employee create latency does **not** include Notification Service availability — that is intentional latency *hiding*, not reduction of the notification work itself.

### How to verify improvements

1. Capture baseline under load (open-loop / constant rate preferred over closed-loop):
   ```bash
   # Example: hey or k6 at fixed RPS; collect X-Response-Time-Ms or server logs
   hey -z 30s -q 50 -m GET http://localhost:5001/api/users
   ```
2. Report **p50 / p95 / p99**, not only mean.
3. Apply one change at a time; re-measure the same way so the metric movement is attributable.
4. If p99 is high but p50 is fine → look at dependency timeout, pool queueing, or GC/lock spikes — not average path micro-optimizations.

## Run

```bash
docker compose up --build
```

| Service | Swagger |
|---------|---------|
| Identity | http://localhost:5001/swagger |
| Employee | http://localhost:5003/swagger |
| Notification | http://localhost:5005/swagger |

## Stack

- ASP.NET Core 8, EF Core, PostgreSQL, gRPC
- TransactionalBox (Outbox/Inbox) + Kafka
- FluentValidation, Serilog, Swagger
- Output caching, response compression, latency middleware

## Layering

```
Domain/ → Application/ (interfaces, services, messages, handlers) → Infrastructure/ → API/
```

SOLID + eventual consistency + explicit latency budgets.
