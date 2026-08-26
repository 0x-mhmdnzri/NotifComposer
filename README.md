# Backend .NET Code Challenge – Employee Management Microservices

سیستم ساده مدیریت کارکنان با معماری Microservice شامل سه سرویس:

- **Identity Service**: مدیریت کاربران
- **Employee Service**: مدیریت کارکنان + ارتباط gRPC با Identity + ارسال اعلان به Notification
- **Notification Service**: ثبت اعلان‌ها

## معماری

```
┌─────────────────┐     gRPC      ┌─────────────────┐
│ Employee        │ ─────────────► │ Identity        │
│ Service         │                │ Service         │
└────────┬────────┘                └─────────────────┘
         │ HTTP (fire-and-forget)
         ▼
┌─────────────────┐
│ Notification    │
│ Service         │
└─────────────────┘
```

- هر سرویس دیتابیس PostgreSQL مستقل دارد.
- قبل از ایجاد کارمند، وجود User از طریق **gRPC** چک می‌شود.
- پس از ایجاد موفق کارمند، یک اعلان به Notification Service ارسال می‌شود.
- اگر Notification در دسترس نباشد، عملیات ثبت کارمند Rollback نمی‌شود و فقط خطا لاگ می‌شود.

## اجرای پروژه

```bash
docker compose up --build
```

پس از بالا آمدن سرویس‌ها:

| سرویس              | Swagger                          | Port  |
|--------------------|----------------------------------|-------|
| Identity Service   | http://localhost:5001/swagger    | 5001  |
| Employee Service   | http://localhost:5003/swagger    | 5003  |
| Notification Service | http://localhost:5005/swagger  | 5005  |

### gRPC Identity
- Address داخل شبکه Docker: `http://identity-service:8081`

## API Overview

### Identity Service
- `POST /api/users` – ایجاد کاربر
- `GET /api/users/{id}` – دریافت کاربر
- `GET /api/users` – لیست کاربران (Pagination + Filtering)

### Employee Service
- `POST /api/employees` – ایجاد کارمند (با چک gRPC)
- `PUT /api/employees/{id}` – ویرایش
- `GET /api/employees/{id}` – دریافت
- `GET /api/employees` – لیست (Pagination + Filtering)
- `PATCH /api/employees/{id}/preferences` – بروزرسانی Preferences (jsonb)

### Notification Service
- `POST /api/notifications` – ثبت اعلان
- `GET /api/notifications/{id}` – دریافت
- `GET /api/notifications` – لیست

## تکنولوژی‌ها
- ASP.NET Core 8
- Entity Framework Core + PostgreSQL (Npgsql)
- gRPC
- FluentValidation
- Docker & Docker Compose
- Swagger / OpenAPI
- Serilog (Logging)

## ساختار پروژه

```
src/
├── IdentityService/
├── EmployeeService/
└── NotificationService/
```

هر سرویس دارای لایه‌های نسبتاً جدا شده (Domain / Application / Infrastructure) است.

## نکات مهم
- تمام تنظیمات از Environment Variable خوانده می‌شوند.
- Migrationها به صورت خودکار در startup اعمال می‌شوند.
- Validation با FluentValidation انجام می‌شود.
- Exception Handling مرکزی وجود دارد.
- Preferences به صورت `jsonb` ذخیره می‌شود.
