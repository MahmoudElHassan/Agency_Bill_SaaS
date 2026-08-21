# Deploy API to Render

## Redis (required for register / login)

Register and login store refresh tokens in Redis. If Redis is wrong, register returns **500**.

On Render → **Environment**, set:

```env
ConnectionStrings__Redis=rediss://default:YOUR_UPSTASH_TOKEN@YOUR_REAL_HOST.upstash.io:6379
```

Example shape (use **your** Upstash host from the console, not a placeholder):

```env
ConnectionStrings__Redis=rediss://default:AXyz...@witty-dinosaur-130239.upstash.io:6379
```

**Do not use:**

| Bad value | Why |
|---|---|
| `your_host.upstash.io` | Placeholder — cannot connect |
| `https://….upstash.io` | REST API, not Redis protocol |
| `redis-cli --tls -u …` alone | Prefer pasting the `rediss://…` URL |

After saving env vars, **Manual Deploy** or restart, then check:

```bash
curl -s https://YOUR-SERVICE.onrender.com/health
```

Both `postgres` and `redis` must be `"Healthy"`.

## Other required env vars

```env
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Default=Host=…;Database=neondb;Username=…;Password=…;SSL Mode=Require
Jwt__Key=at-least-32-random-characters-here
PublicAppUrl=https://agency-bill-saas.vercel.app
Cors__Origins__0=https://agency-bill-saas.vercel.app
Hangfire__Disabled=true
```

## Docker settings

| Setting | Value |
|---|---|
| Root Directory | *(empty)* |
| Dockerfile Path | `src/Ledgerly.Api/Dockerfile` |
| Health Check Path | `/health` |

## Vercel

Set `API_PROXY_URL=https://YOUR-SERVICE.onrender.com` and redeploy.
