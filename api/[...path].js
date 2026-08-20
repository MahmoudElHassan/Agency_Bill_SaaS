/**
 * Proxies /api/* to the .NET backend (API_PROXY_URL or VITE_API_URL on Vercel).
 * Without this, SPA rewrites would 404 on auth/login.
 */
export default async function handler(req, res) {
  const backend = (process.env.API_PROXY_URL || process.env.VITE_API_URL || "").replace(/\/$/, "");

  if (!backend) {
    res.statusCode = 503;
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify({
      success: false,
      error: {
        code: "api_not_configured",
        message: "Set API_PROXY_URL in Vercel to your deployed .NET API URL (e.g. https://your-api.onrender.com)."
      }
    }));
    return;
  }

  const url = new URL(req.url, `http://${req.headers.host}`);
  const target = `${backend}${url.pathname}${url.search}`;

  const headers = { ...req.headers };
  delete headers.host;
  delete headers.connection;

  let body;
  if (req.method !== "GET" && req.method !== "HEAD") {
    const chunks = [];
    for await (const chunk of req) chunks.push(chunk);
    body = Buffer.concat(chunks);
  }

  try {
    const upstream = await fetch(target, {
      method: req.method,
      headers,
      body
    });

    res.statusCode = upstream.status;
    upstream.headers.forEach((value, key) => {
      if (key.toLowerCase() === "transfer-encoding") return;
      res.setHeader(key, value);
    });

    const buf = Buffer.from(await upstream.arrayBuffer());
    res.end(buf);
  } catch (err) {
    res.statusCode = 502;
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify({
      success: false,
      error: {
        code: "upstream_error",
        message: `Cannot reach backend at ${backend}.`
      }
    }));
  }
}
