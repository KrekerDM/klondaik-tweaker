const pending = new Map();
const listeners = new Map();
let seq = 0;

const host = window.chrome && window.chrome.webview ? window.chrome.webview : null;

if (host) {
  host.addEventListener("message", (e) => {
    let msg = e.data;
    if (typeof msg === "string") {
      try {
        msg = JSON.parse(msg);
      } catch {
        return;
      }
    }
    if (msg.evt) {
      const set = listeners.get(msg.evt);
      if (set) set.forEach((fn) => fn(msg.data));
      return;
    }
    const entry = pending.get(msg.id);
    if (!entry) return;
    pending.delete(msg.id);
    clearTimeout(entry.timer);
    if (msg.ok) entry.resolve(msg.result);
    else entry.reject(new Error(msg.error || "unknown error"));
  });
}

export function invoke(method, payload, timeout = 600000) {
  if (!host) return Promise.reject(new Error("host bridge unavailable"));
  const id = "r" + ++seq;
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      pending.delete(id);
      reject(new Error("timeout: " + method));
    }, timeout);
    pending.set(id, { resolve, reject, timer });
    host.postMessage(JSON.stringify({ id, method, payload: payload || {} }));
  });
}

export function on(channel, fn) {
  if (!listeners.has(channel)) listeners.set(channel, new Set());
  listeners.get(channel).add(fn);
  return () => listeners.get(channel).delete(fn);
}

export function send(method, payload) {
  if (!host) return;
  host.postMessage(JSON.stringify({ id: "", method, payload: payload || {} }));
}
