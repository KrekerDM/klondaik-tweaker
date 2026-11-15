import { t, getLang } from "./i18n.js";

export function esc(value) {
  return String(value == null ? "" : value)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

export function h(html) {
  const tpl = document.createElement("template");
  tpl.innerHTML = html.trim();
  return tpl.content.firstElementChild;
}

export function frag(html) {
  const tpl = document.createElement("template");
  tpl.innerHTML = html;
  return tpl.content;
}

export function bytes(value) {
  const n = Number(value) || 0;
  if (n < 1024) return n + " " + t("unit.b");
  if (n < 1024 * 1024) return (n / 1024).toFixed(0) + " " + t("unit.kb");
  if (n < 1024 * 1024 * 1024) return (n / 1024 / 1024).toFixed(n < 10 * 1024 * 1024 ? 1 : 0) + " " + t("unit.mb");
  return (n / 1024 / 1024 / 1024).toFixed(2) + " " + t("unit.gb");
}

export function when(value) {
  const d = new Date(value);
  if (isNaN(d.getTime())) return "-";
  const options = { dateStyle: "short", timeStyle: "short" };
  try {
    return d.toLocaleString(getLang(), options);
  } catch {
    return d.toLocaleString("en-GB", options);
  }
}

export function num(value, digits = 0) {
  const options = { minimumFractionDigits: digits, maximumFractionDigits: digits };
  try {
    return (Number(value) || 0).toLocaleString(getLang(), options);
  } catch {
    return (Number(value) || 0).toLocaleString("en-US", options);
  }
}

export function duration(seconds) {
  const s = Math.max(0, Math.floor(Number(seconds) || 0));
  const d = Math.floor(s / 86400);
  const hrs = Math.floor((s % 86400) / 3600);
  const min = Math.floor((s % 3600) / 60);
  if (d > 0) return d + "d " + hrs + "h";
  if (hrs > 0) return hrs + "h " + min + "m";
  return min + "m";
}

let toastTimer = 0;

export function toast(message, kind = "") {
  const root = document.getElementById("toasts");
  const el = h('<div class="toast ' + kind + '">' + esc(message) + "</div>");
  root.appendChild(el);
  clearTimeout(toastTimer);
  setTimeout(() => {
    el.style.transition = "opacity .3s, transform .3s";
    el.style.opacity = "0";
    el.style.transform = "translateX(20px)";
    setTimeout(() => el.remove(), 320);
  }, kind === "err" ? 6500 : 3800);
}

export function progress(percent) {
  const bar = document.getElementById("progbar");
  if (!bar) return;
  if (percent <= 0 || percent >= 100) {
    bar.style.width = percent >= 100 ? "100%" : "0";
    if (percent >= 100) setTimeout(() => (bar.style.width = "0"), 400);
    return;
  }
  bar.style.width = percent + "%";
}

export function modal(options) {
  return new Promise((resolve) => {
    const root = document.getElementById("modalRoot");
    const actions = (options.actions || [{ id: "ok", label: t("act.close"), primary: true }])
      .map(
        (a) =>
          '<button class="btn ' +
          (a.danger ? "btn-danger" : a.primary ? "btn-primary" : "btn-ghost") +
          '" data-act="' +
          a.id +
          '">' +
          esc(a.label) +
          "</button>"
      )
      .join("");
    const overlay = h(
      '<div class="overlay"><div class="modal stack">' +
        '<h2>' + esc(options.title || "") + "</h2>" +
        '<div class="stack-sm">' + (options.body || "") + "</div>" +
        '<div class="row" style="justify-content:flex-end;margin-top:8px">' + actions + "</div>" +
        "</div></div>"
    );
    const done = (value) => {
      overlay.remove();
      document.removeEventListener("keydown", onKey);
      resolve(value);
    };
    const onKey = (e) => {
      if (e.key === "Escape") done(null);
    };
    overlay.addEventListener("click", (e) => {
      if (e.target === overlay) done(null);
      const btn = e.target.closest("[data-act]");
      if (btn) done(btn.dataset.act);
    });
    document.addEventListener("keydown", onKey);
    root.appendChild(overlay);
  });
}

export async function confirmBox(title, text, confirmLabel, danger) {
  const res = await modal({
    title,
    body: '<p class="lead small">' + esc(text) + "</p>",
    actions: [
      { id: "ok", label: confirmLabel || t("act.confirm"), primary: !danger, danger: !!danger },
      { id: "no", label: t("act.cancel") }
    ]
  });
  return res === "ok";
}

export function switchEl(on, onChange) {
  const el = h('<div class="sw' + (on ? " on" : "") + '" role="switch"></div>');
  el.addEventListener("click", async () => {
    if (el.classList.contains("busy")) return;
    const next = !el.classList.contains("on");
    el.classList.add("busy");
    try {
      const ok = await onChange(next);
      if (ok !== false) el.classList.toggle("on", next);
    } finally {
      el.classList.remove("busy");
    }
  });
  return el;
}

export function checkbox(on) {
  const el = h('<div class="cb' + (on ? " on" : "") + '"></div>');
  el.addEventListener("click", () => el.classList.toggle("on"));
  return el;
}

export function barClass(percent) {
  if (percent >= 90) return "bar crit";
  if (percent >= 70) return "bar hot";
  return "bar";
}
