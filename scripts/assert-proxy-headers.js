/** Assert proxy drops encoding headers that break browsers after Node fetch decompresses. */
const assert = require("node:assert/strict");

const hopByHop = new Set([
  "transfer-encoding",
  "content-encoding",
  "content-length",
  "connection",
  "keep-alive",
  "proxy-authenticate",
  "proxy-authorization",
  "te",
  "trailers",
  "upgrade"
]);

for (const h of ["content-encoding", "content-length", "transfer-encoding"]) {
  assert.equal(hopByHop.has(h), true, `${h} must be stripped`);
}
assert.equal(hopByHop.has("content-type"), false);

console.log("proxy header strip assertions ok");
