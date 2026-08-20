/** Proxies /api/* to the .NET backend. Invoked via vercel.json rewrite for nested paths. */
module.exports = async (req, res) => {
  const backend = (process.env.API_PROXY_URL || process.env.VITE_API_URL || "").replace(/\/$/, "");

  if (!backend) {
    res.statusCode = 503;
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify({
      success: false,
      error: {
        code: "api_not_configured",
        message: "Set API_PROXY_URL in Vercel Environment Variables to your deployed .NET API URL, then redeploy."
      }
    }));
    return;
  }

  const subPath = typeof req.query.path === "string" ? req.query.path : "";
  const url = new URL(req.url || "/", `http://${req.headers.host || "localhost"}`);
  const qs = url.search || "";
  const target = `${backend}/api/${subPath}${qs}`;

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
    const upstream = await fetch(target, { method: req.method, headers, body });
    res.statusCode = upstream.status;
    upstream.headers.forEach((value, key) => {
      if (key.toLowerCase() === "transfer-encoding") return;
      res.setHeader(key, value);
    });
    res.end(Buffer.from(await upstream.arrayBuffer()));
  } catch {
    res.statusCode = 502;
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify({
      success: false,
      error: { code: "upstream_error", message: `Cannot reach backend at ${backend}.` }
    }));
  }
};
