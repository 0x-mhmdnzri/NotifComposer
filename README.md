# Backend .NET Code Challenge – Employee Management Microservices

سیستم مدیریت کارکنان با معماری **Microservice** + **SOLID** + **Transactional Outbox/Inbox** (TransactionalBox).

## معماری

```
┌─────────────────┐     gRPC (sync)      ┌─────────────────┐
│  Employee       │ ───────────────────► │  Identity       │
│  Service        │                      │  Service        │
└────────┬────────┘                      └─────────────────┘
         │
         │ Transactional Outbox (same DB transaction)
         │ EmployeeCreatedEvent
         ▼
    ┌─────────┐
    │  Kafka  │
    └────┬────┘
         │ Transactional Inbox (exactly-once via dedup)
         ▼
┌─────────────────┐
│  Notification   │
│  Service        │
└─────────────────┘
```

### اصول طراحی

| اصل | پیاده‌سازی |
|-----|------------|
| **SRP** | Repository / Application Service / Handler / Client جدا |
| **DIP** | وابستگی به اینترفیس (`IEmployeeRepository`, `IIdentityClient`, ...) |
| **ISP** | اینترفیس‌های کوچک و متمرکز |
| **OCP** | افزودن Handler جدید بدون تغییر کد موجود |
| **Outbox/Inbox** | `TransactionalBox` + EF Core + Kafka |

- قبل از ایجاد کارمند، وجود User از طریق **gRPC** چک می‌شود (sync).
- پس از ایجاد موفق کارمند، رویداد در **همان تراکنش دیتابیس** در Outbox ذخیره می‌شود.
- Background job پیام را به Kafka می‌فرستد (at-least-once).
- NotificationService با Inbox پیام را دریافت و با **exactly-once** (IdempotentInboxKey) پردازش می‌کند.
- اگر Notification یا Kafka موقتاً در دسترس نباشد، ثبت کارمند **Rollback نمی‌شود**.

> ⚠️ TransactionalBox هنوز به‌صورت رسمی Production-ready اعلام نشده (alpha). برای چالش کد و یادگیری مناسب است.

## اجرا

```bash
docker compose up --build
```

| سرویس | Swagger | Port |
|-------|---------|------|
| Identity | http://localhost:5001/swagger | 5001 |
| Employee | http://localhost:5003/swagger | 5003 |
| Notification | http://localhost:5005/swagger | 5005 |
| Kafka | localhost:9092 | 9092 |

## API ها

### Identity
- `POST /api/users`
- `GET /api/users/{id}`
- `GET /api/users?search=&isActive=&page=&pageSize=`

### Employee
- `POST /api/employees` — ایجاد + Outbox event
- `PUT /api/employees/{id}`
- `GET /api/employees/{id}`
- `GET /api/employees?department=&position=&userId=&page=&pageSize=`
- `PATCH /api/employees/{id}/preferences`

### Notification
- `POST /api/notifications` (دستی)
- `GET /api/notifications/{id}`
- `GET /api/notifications?userId=&page=&pageSize=`

## تکنولوژی‌ها

- ASP.NET Core 8
- EF Core + PostgreSQL (Npgsql)
- gRPC (Identity check)
- **TransactionalBox** (Outbox/Inbox) + Kafka
- FluentValidation
- Serilog
- Swagger
- Docker Compose

## ساختار لایه‌ها (هر سرویس)

```
Domain/          → Entities (pure)
Application/     → Interfaces, DTOs, Validators, Services, Messages, Handlers
Infrastructure/  → Persistence (DbContext + Repositories), Clients (gRPC)
API/             → Controllers, Middleware, Program.cs
```
