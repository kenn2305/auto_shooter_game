// Usage: node tcall.js method '{"json":"params"}'   OR   node tcall.js --file calls.json
const http = require("http");
const fs = require("fs");

let calls;
if (process.argv[2] === "--file") {
  calls = JSON.parse(fs.readFileSync(process.argv[3], "utf8"));
} else {
  calls = [{ method: process.argv[2], params: process.argv[3] ? JSON.parse(process.argv[3]) : {} }];
}
const body = JSON.stringify({ calls, timeoutMs: 120000 });
const req = http.request(
  { host: "127.0.0.1", port: 6510, path: "/call", method: "POST", headers: { "Content-Type": "application/json", "Content-Length": Buffer.byteLength(body) } },
  (res) => {
    let d = "";
    res.on("data", (c) => (d += c));
    res.on("end", () => {
      const out = JSON.parse(d);
      for (const r of out) {
        const s = JSON.stringify(r.error ? { error: r.error } : r.result);
        console.log(s.length > 1500 ? s.slice(0, 1500) + "...(truncated)" : s);
      }
    });
  }
);
req.on("error", (e) => console.error("bridge error", e.message));
req.write(body);
req.end();
