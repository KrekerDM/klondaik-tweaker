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

let inFlight = 0;
let busyTimer = null;
const busyWatchers = new Set();

function setBusy(on) {
  busyWatchers.forEach((fn) => {
    try {
      fn(on);
    } catch {}
  });
}

function enter() {
  inFlight++;
  if (inFlight === 1 && !busyTimer) {
    busyTimer = setTimeout(() => {
      busyTimer = null;
      if (inFlight > 0) setBusy(true);
    }, 250);
  }
}

function leave() {
  inFlight = Math.max(0, inFlight - 1);
  if (inFlight === 0) {
    if (busyTimer) {
      clearTimeout(busyTimer);
      busyTimer = null;
    }
    setBusy(false);
  }
}

export function onBusy(fn) {
  busyWatchers.add(fn);
  return () => busyWatchers.delete(fn);
}

export function invoke(method, payload, timeout = 600000) {
  if (!host) return Promise.reject(new Error("host bridge unavailable"));
  const id = "r" + ++seq;
  enter();
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      pending.delete(id);
      leave();
      reject(new Error("timeout: " + method));
    }, timeout);
    pending.set(id, {
      resolve: (v) => {
        leave();
        resolve(v);
      },
      reject: (e) => {
        leave();
        reject(e);
      },
      timer
    });
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
