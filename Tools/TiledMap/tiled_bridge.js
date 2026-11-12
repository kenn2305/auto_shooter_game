// Minimal stand-in for the Tiled MCP Pro server: speaks the extension's HTTP polling protocol.
// Extension side: GET /status, GET /poll -> [jsonrpc requests], POST /response <- jsonrpc response.
// Client side (us): POST /call {calls:[{method, params}], timeoutMs} -> waits for all results.
const http = require("http");

const PORT = 6510;
let nextId = 1;
const queue = [];          // pending requests not yet delivered
const waiters = new Map(); // id -> resolve
let lastPoll = 0;

function readBody(req) {
  return new Promise((resolve) => {
    let data = "";
    req.on("data", (c) => (data += c));
    req.on("end", () => resolve(data));
  });
}

function send(res, code, obj) {
  const body = typeof obj === "string" ? obj : JSON.stringify(obj);
  res.writeHead(code, { "Content-Type": "application/json" });
  res.end(body);
}

http
  .createServer(async (req, res) => {
    const url = new URL(req.url, "http://127.0.0.1");
    if (req.method === "GET" && url.pathname === "/status") {
      return send(res, 200, { ok: true, server: "claude-tiled-bridge" });
    }
    if (req.method === "GET" && url.pathname === "/poll") {
      lastPoll = Date.now();
      const batch = queue.splice(0, queue.length);
      return send(res, 200, batch);
    }
    if (req.method === "POST" && url.pathname === "/response") {
      const body = await readBody(req);
      try {
        const msg = JSON.parse(body);
        const w = waiters.get(msg.id);
        if (w) {
          waiters.delete(msg.id);
          w(msg);
        }
      } catch (e) {
        console.error("bad response", e.message);
      }
      return send(res, 200, { ok: true });
    }
    if (req.method === "GET" && url.pathname === "/health") {
      return send(res, 200, { lastPollAgoMs: lastPoll ? Date.now() - lastPoll : null, queued: queue.length });
    }
    if (req.method === "POST" && url.pathname === "/call") {
      const body = JSON.parse(await readBody(req));
      const timeoutMs = body.timeoutMs || 60000;
      const promises = body.calls.map((c) => {
        const id = nextId++;
        queue.push({ jsonrpc: "2.0", method: c.method, params: c.params || {}, id });
        return new Promise((resolve) => {
          waiters.set(id, resolve);
          setTimeout(() => {
            if (waiters.has(id)) {
              waiters.delete(id);
              resolve({ id, error: { message: "timeout waiting for Tiled" } });
            }
          }, timeoutMs);
        });
      });
      const results = await Promise.all(promises);
      return send(res, 200, results);
    }
    send(res, 404, { error: "not found" });
  })
  .listen(PORT, "127.0.0.1", () => console.log("tiled bridge on " + PORT));
