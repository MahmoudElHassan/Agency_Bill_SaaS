# Decisions and fallbacks

This file captures every non-obvious decision made during the build and any documented fallback used to keep moving.

## Architecture

- **No MediatR.** Per the standing rules. Controllers inject use-case handlers directly. Each handler is a plain class with one `HandleAsync(...)` method.
- **Result<T> + ApiResponse<T>.** Uniform JSON envelope across all endpoints (`{ success, data, error }`). Errors carry a stable `code` (e.g. `plan_limit`) and a human message. The `code` is mapped to HTTP status in `ResultExtensions`.
- **Query-filter tenant isolation.** Every `TenantEntity` carries a global query filter `TenantId == currentTenant.Id` set in `AppDbContext.OnModelCreating`. Disable in webhook handler with `IgnoreQueryFilters()` only when you have a verified `tenantId` from Stripe metadata.
- **HttpContextCurrentTenant** wraps `IHttpContextAccessor` and reads tenant info from `HttpContext.Items` on every property access. Avoids the classic scoped-factory-evaluated-too-early bug where the empty `CurrentTenant` is cached before the auth middleware populates it.

## Auth

- JWT claims are short: `sub`, `email`, `tid`, `role`. `MapInboundClaims = false` on JwtBearer so the claim URIs do not get rewritten.
- Refresh tokens are opaque, base64-urlsafe, 48 bytes, stored in Redis with TTL.
- Password hashing uses BCrypt at work factor 11.

## Stripe

- Checkout Session uses `Mode = "subscription"`, writes `tenantId` in `Metadata`.
- Webhook signature verified by reading the **raw** request body and passing it to `EventUtility.ConstructEvent` together with `Stripe-Signature` and `WebhookSecret`.
- StripeException is caught at the exception middleware and returns 400 instead of 500 so the dashboard does not see a fake crash.
- **Fallback for missing Stripe CLI:** `POST /api/dev/webhook/{tenantId}?type=...&invoiceId=...` invokes the webhook handler directly with a synthetic `StripeEventId`. This is gated behind `Dev:EnableWebhookSimulator` in config.
- Idempotency: the `WebhookEvent` table has a unique index on `StripeEventId`. The handler short-circuits if the row already exists.

## Database

- Connection string uses local Postgres 15 from Homebrew (no Docker for the dev DB).
- All money columns are `decimal(18,2)`. Tax rate is `decimal(8,4)` for percent precision.
- `Migrate()` runs at startup so a fresh `createdb ledgerly` plus `dotnet run` is enough to bootstrap.

## What was dropped

Per the scope-creep fallback in the plan: no PDF generation, no Hangfire in production (only the dashboard route stub), no email transport (console logger only).

## Definition of Done

- [x] Two tenants cannot see each other's data — covered by `TenantIsolationTests.Cross_tenant_get_with_filter_returns_null` plus a curl-based smoke test.
- [x] Free plan blocked at 4th invoice in a month — covered by the live 402 response.
- [x] Duplicate Stripe webhook is idempotent — the `WebhookEvent` unique index plus the early-return in `StripeWebhookHandler` guarantee this.
- [x] `dotnet run` starts API + uses local Postgres + Redis.
- [x] Swagger covers all 18 endpoints.
- [x] README lets a stranger run the project in 15 minutes.
- [x] No Nawah, no portfolio-site files, no charity domain references.