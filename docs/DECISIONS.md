# Decisions and fallbacks

This file captures every non-obvious decision made during the build and any documented fallback used to keep moving.

## Architecture

- **No MediatR.** Per the standing rules. Controllers inject use-case handlers directly. Each handler is a plain class with one `HandleAsync(...)` method.
- **Result<T> + ApiResponse<T>.** Uniform JSON envelope across all endpoints (`{ success, data, error }`). Errors carry a stable `code` (e.g. `plan_limit`) and a human message. The `code` is mapped to HTTP status in `ResultExtensions`.
- **Query-filter tenant isolation.** Every `TenantEntity` carries a strict global query filter `TenantId == currentTenant.Id` set in `AppDbContext.OnModelCreating`. The previous `|| TenantId == Guid.Empty` bypass was removed (Phase A). Disable only in webhook handler with `IgnoreQueryFilters()` after asserting tenant match from Stripe metadata.
- **HttpContextCurrentTenant** wraps `IHttpContextAccessor` and reads tenant info from `HttpContext.Items` on every property access. Avoids the classic scoped-factory-evaluated-too-early bug where the empty `CurrentTenant` is cached before the auth middleware populates it.

## Auth

- JWT claims are short: `sub`, `email`, `tid`, `role`. `MapInboundClaims = false` on JwtBearer so the claim URIs do not get rewritten.
- **Opaque refresh tokens.** The opaque random string is the Redis key. `IRefreshTokenStore.FindUserIdAsync(token)` returns the user id. `RevokeAsync(token)` deletes the key. Old behavior that parsed the refresh as JWT was removed.
- `POST /api/auth/refresh` and `POST /api/auth/logout` are `[AllowAnonymous]` because the refresh token in the body carries the user identity; no JWT required.
- `GET /api/auth/me` is `[Authorize]`.
- **Global unique email.** New unique index on `User.Email` (was `{TenantId, Email}`). Migration applied 20260814195706.
- **Owner RBAC.** `POST /api/billing/checkout`, `POST /api/billing/portal`, `DELETE /api/clients/{id}`, and `POST /api/invoices/{id}/void` all require `[Authorize(Roles = "Owner")]`.
- **JWT key guard.** Outside Development, Program.cs throws at startup if `Jwt:Key` is empty, shorter than 32 characters, or equal to the well-known `dev_only_change_me…` placeholder.

## Stripe

- Checkout Session uses `Mode = "subscription"` and writes `tenantId` to both `Metadata` and `SubscriptionData.Metadata` so subscription events carry it.
- Webhook signature verified by reading the **raw** request body and passing it to `EventUtility.ConstructEvent` together with `Stripe-Signature` and `WebhookSecret`.
- The webhook controller now `switch`es on `Event.Data.Object` to extract `tenantId`, `invoiceId`, `customerId`, `subscriptionId`, `priceId`, `paymentIntentId` from `PaymentIntent`, `Checkout.Session`, or `Subscription`. A `StripeWebhookPayload` record is passed into the handler.
- The handler applies the billing/payment side-effects:
  - `checkout.session.completed` → set `StripeCustomerId`, `StripeSubscriptionId`, `PlanStatus = Active`.
  - `customer.subscription.updated` → map `priceId` to plan via `StripePriceOptions` (`PlanCatalog.FromPriceId`).
  - `customer.subscription.deleted` → `Plan = Free`, `PlanStatus = Canceled`, clear subscription id.
  - `payment_intent.succeeded` → load invoice ignoring filters, assert tenant match, set `Paid` + `PaidAt` + `StripePaymentIntentId`.
- StripeException is caught at the exception middleware and returns 400 instead of 500 so the dashboard does not see a fake crash.

## Dev webhook simulator

- `POST /api/dev/webhook/{tenantId}` and `POST /api/dev/jobs/mark-overdue` are gated on **both** `app.Environment.IsDevelopment()` AND `Dev:EnableWebhookSimulator` (or `Hangfire:UseInMemory` for the job). Returns 404 otherwise.
- `Dev:EnableWebhookSimulator` is `false` in `appsettings.json` (production default) and `true` in `appsettings.Development.json`.

## Invoices

- **Unique invoice number race.** `InvoiceRepository.AddWithUniqueNumberRetryAsync` catches the `IX_Invoices_TenantId_Number` unique violation, clears the change tracker, recomputes the next sequence, and retries up to 3 times.
- **ClearLines()** now calls `RecalculateTotals()` so the totals zero out.
- **VoidInvoiceHandler** returns `InvalidState` if the invoice is already Paid.

## Hangfire

- Registered with `AddHangfire` + `AddHangfireServer`. PostgreSQL storage in production, in-memory storage in Development (when `Hangfire:UseInMemory=true`).
- Recurring job `mark-overdue-hourly` runs hourly and calls `MarkOverdueInvoicesHandler`, which flips Draft/Sent invoices past due to Overdue and fires the email-sender stub.
- Dashboard at `/hangfire` is Development only.
- Tests opt out via `Hangfire:Disabled=true`.

## Database

- Local Postgres 15 (Homebrew) for dev; `appsettings.json` uses generic placeholders (`postgres`/`postgres`).
- All money columns are `decimal(18,2)`. Tax rate is `decimal(8,4)` for percent precision.
- `Migrate()` runs at startup in Development only. Other environments do not auto-migrate.

## What was dropped

Per the scope-creep fallback in the original plan: no PDF generation, no email transport (console logger only), no Azure deploy.

## Fixes from the code-review pass (Phase A-F)

| Phase | What was fixed |
| ----- | -------------- |
| A | Dev simulator no longer reachable in Production; JWT-only tenant middleware (query-string tenant override removed); strict query filters (`Guid.Empty` bypass removed); config hygiene (machine username, JWT key guard, Swagger Dev, Migrate Dev). |
| B | Opaque refresh tokens lookup-able by id; global unique email; Owner RBAC on billing/checkout/portal/delete/void; `/me` is `[Authorize]`. |
| C | Webhook controller parses real Stripe.net objects (`PaymentIntent`, `Session`, `Subscription`); handler applies billing + payment; invoice number race handled with retry; `ClearLines()` recalculates; void Paid returns `InvalidState`. |
| D | Hangfire registered with recurring overdue job and Dev in-memory fallback. |
| E | HTTP integration tests for tenant isolation (cross-tenant 404, query-string ignored), plan limit (402), refresh (rotates, reuse 401), webhook idempotency, dev simulator gating. Unit tests for query filter (`Guid.Empty` returns nothing), `ClearLines` zeros totals, void paid → `InvalidState`. |
| F | DECISIONS.md and README updated; frontend `api.ts` gets a `refresh()` helper plus automatic 401 retry. |

## Definition of Done

- [x] `dotnet test` green, including the new HTTP tests.
- [x] `GET /api/dev/webhook/...` 404 when `ASPNETCORE_ENVIRONMENT=Production` (or flag false).
- [x] Forged `?tenantId=` cannot change tenant for an authenticated user (covered by `Tenant_query_param_does_not_override_jwt`).
- [x] Refresh with the login refresh token returns 200 (covered by `Register_login_me_refresh_logout_round_trip`).
- [x] Duplicate webhook event id does not double-pay (covered by `Same_stripe_event_id_marks_invoice_paid_only_once`).
- [x] README matches the running app (Hangfire, Swagger, simulator).