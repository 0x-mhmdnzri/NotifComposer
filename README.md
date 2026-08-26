# Backend .NET Code Challenge – Employee Management Microservices

Microservice system with **SOLID**, **Transactional Outbox/Inbox**, **latency engineering**, and **security hardening**.

## Security (post security-review)

Findings were filed as GitHub Issues #1–#6 and remediated as follows:

| Issue | Severity | Fix |
|-------|----------|-----|
| #1 No authentication | Critical | API Key auth (`X-Api-Key`) on all REST controllers |
| #2 Verbose exceptions | High | Generic 5xx messages; details only in logs |
| #3 Weak DB credentials | High | Non-default user/password via env; `.env.example`; DB ports not published |
| #4 No rate limiting | Medium | Fixed-window rate limits (60/min API, 20/min writes) |
| #5 Missing headers / CORS | Medium | Security headers middleware; CORS deny-by-default |
| #6 Public gRPC + always-on Swagger | Medium | gRPC host port removed; Swagger gated by `Swagger__Enabled` |

### Calling the APIs

```http
GET http://localhost:5001/api/users
X-Api-Key: dev-api-key-change-me-in-production
```

Default key for local docker-compose: `dev-api-key-change-me-in-production`  
Override with env `API_KEY`. See `.env.example`.

### Health
`GET /health` remains anonymous (for orchestrators).

### Remaining residual risk (documented, not fully eliminated in this challenge)
- Traffic is HTTP (not TLS) inside/out of compose — use a reverse proxy with TLS in real deployments.
- Kafka is plaintext on the internal network.
- API Key is shared-secret auth (not per-user OAuth/JWT roles). Sufficient for service-to-operator protection; add OIDC/JWT for multi-tenant user auth if required.
- gRPC between services has no mTLS (trusts Docker network boundary).

## Architecture

```
Client --X-Api-Key--> REST APIs
Employee --gRPC (internal only)--> Identity
Employee --Outbox--> Kafka --Inbox--> Notification
```

## Run

```bash
cp .env.example .env   # optional; set strong secrets
docker compose up --build
```

| Service | Swagger | Port |
|---------|---------|------|
| Identity | http://localhost:5001/swagger | 5001 |
| Employee | http://localhost:5003/swagger | 5003 |
| Notification | http://localhost:5005/swagger | 5005 |

In Swagger UI, use **Authorize** and paste the API key.

## Stack

ASP.NET Core 8 · EF Core · PostgreSQL · gRPC · TransactionalBox + Kafka · FluentValidation · Serilog · Rate limiting · API Key auth · Output cache · Security headers
