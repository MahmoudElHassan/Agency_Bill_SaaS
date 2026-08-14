# Ledgerly — Demo click path (10 steps)

Use this script for portfolio recordings. All steps assume the API is running at `http://localhost:5080` and the React UI at `http://localhost:5173`.

### 1. Open Swagger
Go to `http://localhost:5080/swagger`. Mention 18 endpoints visible.

### 2. Register a tenant
`POST /api/auth/register`
```json
{ "email": "demo@agency.test", "password": "demo12345", "fullName": "Demo Owner", "tenantName": "Demo Agency" }
```
Save the `accessToken` from the response.

### 3. Authorize in Swagger
Click **Authorize** at the top of Swagger, paste `Bearer <token>`.

### 4. Show `/api/auth/me`
Returns user + tenant + plan.

### 5. Create a client
`POST /api/clients`
```json
{ "name": "Acme Corp", "email": "billing@acme.test", "address": "123 Main St", "currency": "USD" }
```

### 6. Create an invoice
`POST /api/invoices`
```json
{
  "clientId": "<from step 5>",
  "issueDate": "2026-08-14T00:00:00Z",
  "dueDate": "2026-09-14T00:00:00Z",
  "currency": "USD",
  "lines": [{ "description": "Web design sprint", "quantity": 1, "unitPrice": 1500, "taxRate": 10 }]
}
```
Show the auto-generated `number` (`INV-YYYY-####`) and the totals (subtotal 1500, tax 150, total 1650).

### 7. Send the invoice
`POST /api/invoices/{id}/send` — status flips to Sent. The email stub writes a log line.

### 8. Open the public pay link
From the invoice `publicPayToken`, GET `/api/public/invoices/{token}` — returns the customer-facing payload with no auth required.

### 9. Demonstrate plan limit
Create 3 invoices (Free plan allows 3/month). The 4th `POST /api/invoices` returns HTTP 402 with `{ "code": "plan_limit" }`.

### 10. Cross-tenant isolation
Register a second agency, log in as that user, try to GET the first agency's client. Returns 404 — query filter at the EF level hides it.

### Bonus: Stripe (if test keys are configured)
- `POST /api/billing/checkout` with a Pro price ID → opens Stripe Checkout
- Pay with card `4242 4242 4242 4242`
- `POST /api/webhooks/stripe` receives `checkout.session.completed`, marks the tenant plan Active
- Use `POST /api/dev/webhook/{tenantId}?type=checkout.session.completed` if you do not have Stripe CLI installed
- Replaying the same webhook with the same `StripeEventId` is a no-op (idempotency table)

## Quick smoke script

```bash
# 1. Register
TOKEN=$(curl -s -X POST http://localhost:5080/api/auth/register \
  -H 'Content-Type: application/json' \
  -d '{"email":"demo@agency.test","password":"demo12345","fullName":"Demo","tenantName":"Demo Agency"}' \
  | jq -r .data.accessToken)

# 2. Create client
CID=$(curl -s -X POST http://localhost:5080/api/clients \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"name":"Acme","email":"a@a.test","currency":"USD"}' | jq -r .data.id)

# 3. Create + send invoice
INV=$(curl -s -X POST http://localhost:5080/api/invoices \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d "{\"clientId\":\"$CID\",\"issueDate\":\"2026-08-14T00:00:00Z\",\"dueDate\":\"2026-09-14T00:00:00Z\",\"currency\":\"USD\",\"lines\":[{\"description\":\"Web\",\"quantity\":1,\"unitPrice\":1500,\"taxRate\":10}]}")
echo "$INV" | jq
IID=$(echo "$INV" | jq -r .data.id)
curl -s -X POST http://localhost:5080/api/invoices/$IID/send -H "Authorization: Bearer $TOKEN" | jq .data.status
```