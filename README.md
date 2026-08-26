# Backend .NET Code Challenge – Employee Management Microservices

سیستم ساده مدیریت کارکنان با معماری **Microservice** شامل سه سرویس مستقل:

| سرویس | مسئولیت |
|-------|---------|
| **Identity Service** | مدیریت کاربران + gRPC |
| **Employee Service** | مدیریت کارکنان + چک gRPC + ارسال اعلان |
| **Notification Service** | ثبت اعلان‌ها |

## معماری

```
┌─────────────────┐     gRPC (UserExists)     ┌─────────────────┐
│  Employee       │ ─────────────────────────► │  Identity       │
│  Service        │                            │  Service        │
└────────┬────────┘                            └─────────────────┘
         │ HTTP fire-and-forget
         │ (اگر در دسترس نباشد فقط Log می‌شود)
         ▼
┌─────────────────┐
│  Notification   │
│  Service        │
└─────────────────┘
```

- هر سرویس **دیتابیس PostgreSQL مستقل** دارد.
- قبل از ایجاد کارمند، وجود User از طریق **gRPC** بررسی می‌شود.
- پس از ایجاد موفق کارمند، یک اعلان به Notification Service ارسال می‌شود.
- اگر Notification در دسترس نباشد، عملیات ثبت کارمند **Rollback نمی‌شود** و فقط خطا لاگ می‌شود.

## اجرای پروژه

فقط یک دستور:

```bash
docker compose up --build
```

پس از بالا آمدن سرویس‌ها:

| سرویس                 | Swagger URL                        | Port |
|-----------------------|------------------------------------|------|
| Identity Service      | http://localhost:5001/swagger      | 5001 |
| Employee Service      | http://localhost:5003/swagger      | 5003 |
| Notification Service  | http://localhost:5005/swagger      | 5005 |

- gRPC Identity (داخل شبکه Docker): `http://identity-service:8081`

## API های اصلی

### Identity Service
- `POST /api/users` – ایجاد کاربر  
  Body: `{ "fullName": "علی رضایی", "mobile": "09121234567" }`
- `GET /api/users/{id}` – دریافت کاربر
- `GET /api/users?search=&isActive=&page=1&pageSize=10` – لیست + Pagination + Filtering

### Employee Service
- `POST /api/employees` – ایجاد کارمند (با چک gRPC)  
  Body مثال:
  ```json
  {
    "userId": "...",
    "department": "IT",
    "position": "Backend Developer",
    "employmentDate": "2024-01-15T00:00:00Z",
    "preferences": { "language": "fa", "theme": "dark", "receiveEmail": true, "receiveSms": false }
  }
  ```
- `PUT /api/employees/{id}` – ویرایش
- `GET /api/employees/{id}` – دریافت
- `GET /api/employees?department=&position=&userId=&page=1&pageSize=10` – لیست
- `PATCH /api/employees/{id}/preferences` – بروزرسانی Preferences (jsonb)

### Notification Service
- `POST /api/notifications` – ثبت اعلان
- `GET /api/notifications/{id}` – دریافت
- `GET /api/notifications?userId=&page=1&pageSize=10` – لیست

همه APIها Response Schema دارند (Swagger).

## قابلیت‌های پیاده‌سازی شده

- CRUD کامل
- FluentValidation
- Pagination + Filtering
- Logging (Serilog)
- Exception Handling مرکزی
- gRPC بین Employee و Identity
- Fire-and-forget به Notification (بدون rollback)
- Preferences به صورت **jsonb** در PostgreSQL
- EF Core Migration (auto-apply در startup)
- تمام تنظیمات از **Environment Variable**
- Docker + Docker Compose کامل

## تکنولوژی‌ها

- ASP.NET Core 8
- Entity Framework Core + Npgsql
- PostgreSQL 16
- gRPC
- FluentValidation
- Serilog
- Swagger / OpenAPI
- Docker & Docker Compose

## ساختار پروژه

```
├── docker-compose.yml
├── README.md
└── src/
    ├── IdentityService/
    │   ├── Domain/
    │   ├── Application/
    │   ├── Infrastructure/
    │   ├── Controllers/
    │   ├── Grpc/
    │   ├── Protos/
    │   └── Migrations/
    ├── EmployeeService/
    │   ├── Domain/
    │   ├── Application/
    │   ├── Infrastructure/
    │   ├── Controllers/
    │   ├── Protos/
    │   └── Migrations/
    └── NotificationService/
        ├── Domain/
        ├── Application/
        ├── Infrastructure/
        ├── Controllers/
        └── Migrations/
```

لایه‌بندی نسبتاً سبک DDD رعایت شده (Entity با behavior، جداسازی مسئولیت‌ها).

## نکته امنیتی

اگر Personal Access Token را در چت یا جایی به اشتراک گذاشته‌اید، فوراً آن را از GitHub revoke کنید.
