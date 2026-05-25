# DZ_HealthChecks — ASP.NET Core Web API

REST API с аутентификацией через JWT и мониторингом состояния сервисов через HealthChecks.

## Технологии

- **ASP.NET Core 10** — веб-фреймворк
- **Entity Framework Core + Npgsql** — ORM для работы с PostgreSQL
- **JWT Bearer** — аутентификация и авторизация
- **MinIO** — S3-совместимое объектное хранилище
- **BCrypt.Net-Next** — хеширование паролей

## Структура проекта

```
DZ_HealthChecks/
├── Controllers/
│   ├── AuthController.cs       # регистрация и вход
│   └── ProductsController.cs   # CRUD для продуктов
├── Data/
│   └── AppDbContext.cs         # EF Core контекст
├── DTOs/
│   ├── AuthDtos.cs             # RegisterDto, LoginDto
│   └── ProductDtos.cs          # ProductCreateDto, ProductUpdateDto
├── HealthChecks/
│   └── MinioHealthCheck.cs     # собственная реализация IHealthCheck
├── Models/
│   ├── Product.cs
│   └── User.cs
├── Services/
│   └── TokenService.cs         # генерация JWT
├── Program.cs
└── appsettings.json
```

## Требования

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL (локально или в Docker)
- MinIO (локально или в Docker) — опционально

## Запуск

### 1. PostgreSQL

```bash
docker run -d \
  --name postgres \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=healthchecks_db \
  -p 5432:5432 \
  postgres:latest
```

### 2. MinIO

```bash
docker run -d \
  --name minio \
  -e MINIO_ROOT_USER=minioadmin \
  -e MINIO_ROOT_PASSWORD=minioadmin \
  -p 9000:9000 \
  minio/minio server /data
```

### 3. Настройка подключений

Отредактировать `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=healthchecks_db;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Secret": "super-secret-jwt-key-at-least-32-characters-long!",
    "Issuer": "DZ_HealthChecks",
    "Audience": "DZ_HealthChecks"
  },
  "MinIO": {
    "Endpoint": "localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin",
    "UseSSL": "false"
  }
}
```

### 4. Запуск приложения

```bash
dotnet run --project DZ_HealthChecks
```

База данных создаётся автоматически при первом запуске (`EnsureCreated`).

---

## API

### Аутентификация

#### Регистрация

```http
POST /api/auth/register
Content-Type: application/json

{
  "username": "admin",
  "password": "password123"
}
```

**Ответ `200 OK`:**
```json
{ "message": "User registered successfully." }
```

#### Вход

```http
POST /api/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "password123"
}
```

**Ответ `200 OK`:**
```json
{ "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." }
```

---

### Продукты

Все эндпоинты защищены — требуют заголовка:
```
Authorization: Bearer <token>
```

| Метод    | URL                  | Описание              |
|----------|----------------------|-----------------------|
| `GET`    | `/api/products`      | Список всех продуктов |
| `GET`    | `/api/products/{id}` | Продукт по ID         |
| `POST`   | `/api/products`      | Создать продукт       |
| `PUT`    | `/api/products/{id}` | Обновить продукт      |
| `DELETE` | `/api/products/{id}` | Удалить продукт       |

#### Создание продукта

```http
POST /api/products
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "Ноутбук",
  "description": "Игровой ноутбук",
  "price": 75000.00,
  "stock": 10
}
```

**Ответ `201 Created`:**
```json
{
  "id": 1,
  "name": "Ноутбук",
  "description": "Игровой ноутбук",
  "price": 75000.00,
  "stock": 10
}
```

#### Обновление продукта

```http
PUT /api/products/1
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "Ноутбук Pro",
  "description": "Обновлённая модель",
  "price": 85000.00,
  "stock": 5
}
```

**Ответ `204 No Content`**

#### Удаление продукта

```http
DELETE /api/products/1
Authorization: Bearer <token>
```

**Ответ `204 No Content`**

---

## HealthChecks

```http
GET /health
```

**Ответ при всех сервисах в норме:**
```json
{
  "status": "Healthy",
  "results": {
    "Postgres HealthCheck": {
      "status": "Healthy",
      "tags": ["db", "ready"]
    },
    "MinIO HealthCheck": {
      "status": "Healthy",
      "tags": ["ready", "minio"]
    }
  }
}
```

**Ответ при недоступном сервисе:**
```json
{
  "status": "Unhealthy",
  "results": {
    "Postgres HealthCheck": {
      "status": "Healthy",
      "tags": ["db", "ready"]
    },
    "MinIO HealthCheck": {
      "status": "Unhealthy",
      "description": "MinIO is not accessible.",
      "tags": ["ready", "minio"]
    }
  }
}
```

### Реализация HealthChecks

**PostgreSQL** — встроенный через пакет `AspNetCore.HealthChecks.NpgSql`:
```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: connectionString,
        name: "Postgres HealthCheck",
        tags: new[] { "db", "ready" });
```

**MinIO** — собственная реализация `IHealthCheck`:
```csharp
builder.Services.AddHealthChecks()
    .AddCheck<MinioHealthCheck>(
        "MinIO HealthCheck",
        tags: new[] { "ready", "minio" });
```

Класс `MinioHealthCheck` подключается к MinIO и вызывает `ListBucketsAsync`. Если соединение успешно — возвращает `Healthy`, иначе — `Unhealthy` с описанием ошибки.
