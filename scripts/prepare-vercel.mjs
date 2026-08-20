import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const apiUrl = (process.env.API_PROXY_URL || process.env.VITE_API_URL || "").replace(/\/$/, "");

function patchVercelJson(relativePath, spaRewrite) {
  const filePath = path.join(root, relativePath);
  if (!fs.existsSync(filePath)) return;

  const config = JSON.parse(fs.readFileSync(filePath, "utf8"));
  const rewrites = [];

  if (apiUrl) {
    rewrites.push({ source: "/api/(.*)", destination: `${apiUrl}/api/$1` });
    console.log(`${relativePath}: proxy /api/* → ${apiUrl}/api/*`);
  } else {
    rewrites.push({ source: "/api/(.*)", destination: "/api/proxy?path=$1" });
    console.warn(`${relativePath}: API_PROXY_URL not set — /api/* returns 503 until configured.`);
  }

  rewrites.push(spaRewrite);
  config.rewrites = rewrites;
  fs.writeFileSync(filePath, JSON.stringify(config, null, 2) + "\n");
}

patchVercelJson("vercel.json", {
  source: "/((?!assets/|favicon\\.svg|icons\\.svg|api).*)",
  destination: "/index.html"
});

patchVercelJson("frontend/vercel.json", {
  source: "/((?!assets/|favicon\\.svg|icons\\.svg|api).*)",
  destination: "/index.html"
});
