# Agency_Bill_SaaS

A multi-tenant B2B invoicing SaaS for small agencies.
Built with ASP.NET Core 9 + Clean Architecture, PostgreSQL 16, Redis 7, Stripe Billing.

## Why this exists

Small agencies need to send invoices, get paid, and not worry about the plumbing. Ledgerly gives them:
- A tenant workspace per agency
- Client + invoice CRUD
- A public pay link per invoice
- Stripe Billing (Free / Pro / Business)
- Email reminders (stub in dev)
- Multi-tenant isolation enforced at the EF Core query-filter level

## Architecture

```
Ledgerly.sln
src/
  Ledgerly.Domain         entities, enums, no framework deps
  Ledgerly.Application    use-case classes (CreateInvoiceHandler), DTOs, interfaces
  Ledgerly.Infrastructure EF Core, JWT, Stripe, Hangfire, Redis
  Ledgerly.Api            thin controllers, middleware, Swagger, health checks
  Ledgerly.Shared         Result, Error, Guard, PagedResult, ApiResponse
tests/
  Ledgerly.UnitTests          in-memory EF + pure domain tests
  Ledgerly.IntegrationTests   WebApplicationFactory smoke tests
frontend/                React + Vite + TypeScript demo UI
```

Layer rules:
- Domain: no EF, no ASP.NET.
- Application: depends on Domain + Shared only. No EF, no HTTP.
- Infrastructure: implements Application abstractions. EF, Stripe, Hangfire, Redis, JWT.
- Api: thin controllers that inject Application handlers directly. No MediatR.
- Controllers call handlers directly. `Result<T>` becomes a uniform `ApiResponse<T>` JSON envelope.

## Run locally

```bash
# 1. Start Postgres locally (already running on :5432 if you used Homebrew)
brew services start postgresql@15
createdb ledgerly

# 2. Start Redis locally
redis-server --daemonize yes --port 6379

# 3. Run the API
cd src/Ledgerly.Api
dotnet run
# API at http://localhost:5080
# Swagger at http://localhost:5080/swagger
# Health at http://localhost:5080/health

# 4. Run the frontend
cd frontend
npm install
npm run dev
# Frontend at http://localhost:5173
```

## Environment variables

Copy `.env.example` to `.env` and edit. The API also reads from `appsettings.json`.

| Key | Required | Notes |
| --- | --- | --- |
| `ConnectionStrings__Default` | yes | Postgres connection string |
| `ConnectionStrings__Redis` | yes | Redis connection string |
| `Jwt__Key` | yes | 32+ char signing key. Throws at startup outside Development if empty, short, or the well-known placeholder. |
| `Jwt__Issuer`, `Jwt__Audience` | no | defaults to `ledgerly` |
| `Stripe__SecretKey` | for checkout | test mode secret key |
| `Stripe__WebhookSecret` | for webhooks | from `stripe listen` |
| `Stripe__PricePro` / `Stripe__PriceBusiness` | for checkout | Stripe price IDs used to map subscription events to plans |
| `PublicAppUrl` | no | base URL of the frontend (for pay links) |
| `Dev__EnableWebhookSimulator` | no | `true` only in Development. `false` in `appsettings.json`. |
| `Hangfire__UseInMemory` | no | `true` in Development; production uses PostgreSQL storage. |

## API contract

### Auth
- `POST /api/auth/register` — creates Tenant + Owner
- `POST /api/auth/login` — returns JWT + refresh token
- `POST /api/auth/refresh` — rotate JWT using refresh token
- `POST /api/auth/logout` — blacklist the refresh token in Redis
- `GET /api/auth/me` — current user + tenant + plan

### Billing
- `GET /api/billing/plans` — public list of plans
- `POST /api/billing/checkout` — creates Stripe Checkout Session
- `POST /api/billing/portal` — creates Stripe Customer Portal session
- `GET /api/billing/status` — current tenant's plan + status

### Clients
- `GET/POST /api/clients`
- `GET/PUT/DELETE /api/clients/{id}`

### Invoices
- `GET /api/invoices` — filter `?status=`, paging
- `POST /api/invoices`
- `GET /api/invoices/{id}`
- `PUT /api/invoices/{id}` — Draft only
- `POST /api/invoices/{id}/send`
- `POST /api/invoices/{id}/void`
- `GET /api/public/invoices/{token}` — no auth, returns pay-page payload
- `POST /api/public/invoices/{token}/pay` — creates a PaymentIntent

### Webhooks
- `POST /api/webhooks/stripe` — raw body + `Stripe-Signature` header

### Health
- `GET /health`

All authenticated endpoints use `Authorization: Bearer <jwt>`. The JWT carries `tid` (tenant id), `sub` (user id), `role`.
All authenticated endpoints use `Authorization: Bearer <jwt>`. The JWT carries `tid` (tenant id), `sub` (user id), `role`.

## Auth flow

- `POST /api/auth/register` and `POST /api/auth/login` return `{ accessToken, refreshToken, ... }`.
- The opaque `refreshToken` is the Redis key. `POST /api/auth/refresh` with it returns 200 + new tokens; replaying the old refresh returns 401.
- `POST /api/auth/logout` blacklists the refresh token in Redis.
- `GET /api/auth/me` requires `[Authorize]`.

## Hangfire

- Dashboard at `/hangfire` (Development only).
- Recurring job `mark-overdue-hourly` flips Draft/Sent invoices past due to `Overdue` and fires the email-sender stub.
- Dev uses in-memory storage; production uses PostgreSQL storage on the same connection string.

## Dev-only endpoints

- `POST /api/dev/webhook/{tenantId}?type=...&customerId=...&subscriptionId=...&priceId=...&paymentIntentId=...` — invokes `StripeWebhookHandler` directly. Gated on `IsDevelopment()` AND `Dev:EnableWebhookSimulator=true`. Returns 404 otherwise.
- `POST /api/dev/jobs/mark-overdue` — runs `MarkOverdueInvoicesHandler` on demand. Gated on `IsDevelopment()`.

## Done so far

- [x] Day 1: solution, Clean Architecture, Docker Compose, health checks, Swagger, migration
- [x] Day 2: register/login/refresh JWT, tenant isolation middleware, cross-tenant 404 verified
- [x] Day 3: client + invoice CRUD, public pay link, status machine, plan limit returns 402
- [x] Day 4: Stripe Checkout + Customer Portal + idempotent webhook handler + dev simulator
- [x] Day 5: xUnit tests (unit + integration), React demo UI, Swagger, README, DEMO.md
- [x] Day 6: production-style Docker Compose, seed script, Definition of Done checklist
- [x] Review fixes A: dev simulator gated, JWT-only tenant, strict query filters, config hygiene
- [x] Review fixes B: opaque refresh tokens in Redis, global unique email, Owner RBAC
- [x] Review fixes C: real Stripe webhook parsing, billing/payment side-effects, invoice number retry, void guards
- [x] Review fixes D: Hangfire recurring overdue job (Dev in-memory, Prod PostgreSQL)
- [x] Review fixes E: HTTP tests for isolation, 402, refresh, webhook idempotency, dev simulator gating
- [x] Review fixes F: DECISIONS.md, README, frontend refresh-on-401 glue

## Tests

```bash
dotnet test                       # runs both UnitTests and IntegrationTests
dotnet test tests/Ledgerly.UnitTests
```

Unit tests cover:
- Invoice totals (subtotal, tax, total) including `ClearLines` zeros totals
- Tenant filter on `ClientRepository`
- `Guid.Empty` current tenant returns nothing (strict query filter)
- `VoidInvoiceHandler` rejects Paid, accepts Draft
- Plan catalog parsing + price→plan mapping

Integration tests cover:
- `/health` returns 200
- Full auth flow: register, login, `/me` (unauth 401, auth 200), refresh rotates tokens, old refresh 401, logout invalidates
- Tenant isolation: cross-tenant GET 404, `?tenantId=` ignored when JWT present
- Plan limit: 4th invoice in a Free month returns 402 + `code: plan_limit`
- Webhook idempotency: same `StripeEventId` only marks invoice Paid once
- Dev simulator: `/api/dev/webhook/...` 404 in Production; also 404 in Development when `Dev:EnableWebhookSimulator=false`

## Notes / decisions

See `docs/DECISIONS.md` for the troubleshooting log + documented fallbacks used during the build.
