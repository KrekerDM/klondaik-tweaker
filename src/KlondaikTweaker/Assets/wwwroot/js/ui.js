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

const SIGNS = {
  dash:
    '<path class="o" d="M4 18a8 8 0 1 1 16 0"/>' +
    '<rect class="on" x="11" y="10" width="2" height="8" transform="rotate(-35 12 18)"/>',
  wizard:
    '<circle class="on" cx="12" cy="5" r="3"/><circle class="o" cx="12" cy="12" r="3"/>' +
    '<circle class="o" cx="12" cy="19" r="3"/>',
  tweaks:
    '<rect x="3" y="6" width="18" height="2"/><rect class="on" x="13" y="3" width="4" height="8"/>' +
    '<rect x="3" y="16" width="18" height="2"/><rect x="7" y="13" width="4" height="8"/>',
  clean: '<rect class="o" x="5" y="4" width="14" height="16"/><rect class="on" x="7" y="15" width="10" height="3"/>',
  tools: '<rect class="o" x="3" y="10" width="11" height="4"/><circle class="on" cx="18" cy="12" r="3"/>',
  startup: '<rect x="3" y="18" width="18" height="2"/><rect class="on" x="10" y="4" width="4" height="10"/>',
  nvidia: '<rect class="o" x="3" y="5" width="18" height="12"/><rect class="on" x="9" y="9" width="6" height="4"/>',
  power: '<circle class="o" cx="12" cy="13" r="7"/><rect class="on" x="11" y="2" width="2" height="9"/>',
  nic:
    '<rect class="o" x="3" y="6" width="18" height="10"/><rect x="7" y="16" width="2" height="5"/>' +
    '<rect class="on" x="11" y="16" width="2" height="5"/><rect x="15" y="16" width="2" height="5"/>',
  irq:
    '<rect class="o" x="8" y="8" width="8" height="8"/><rect class="on" x="11" y="2" width="2" height="5"/>' +
    '<rect x="11" y="17" width="2" height="5"/><rect x="2" y="11" width="5" height="2"/>' +
    '<rect x="17" y="11" width="5" height="2"/>',
  tasks:
    '<rect x="3" y="5" width="18" height="2"/><rect class="on" x="3" y="11" width="12" height="2"/>' +
    '<rect x="3" y="17" width="18" height="2"/>',
  features:
    '<rect class="o" x="4" y="4" width="7" height="7"/><rect class="o" x="13" y="4" width="7" height="7"/>' +
    '<rect class="o" x="4" y="13" width="7" height="7"/><rect class="on" x="13" y="13" width="7" height="7"/>',
  services:
    '<circle class="o" cx="12" cy="12" r="9"/><circle class="o" cx="12" cy="12" r="5.5"/>' +
    '<circle class="on" cx="12" cy="12" r="2.5"/>',
  apps: '<rect class="o" x="3" y="3" width="12" height="12"/><rect class="on" x="9" y="9" width="12" height="12"/>',
  network:
    '<circle class="o" cx="12" cy="12" r="9"/><path class="o" d="M12 3c3.6 3.6 3.6 14.4 0 18"/>' +
    '<rect class="on" x="3" y="11" width="18" height="2"/>',
  bench:
    '<rect x="4" y="13" width="4" height="7"/><rect x="10" y="9" width="4" height="11"/>' +
    '<rect class="on" x="16" y="4" width="4" height="16"/>',
  soft: '<rect class="o" x="3" y="8" width="18" height="12"/><rect class="on" x="3" y="4" width="18" height="3"/>',
  journal:
    '<circle class="on" cx="5" cy="6" r="2"/><rect x="9" y="5" width="12" height="2"/>' +
    '<circle class="o" cx="5" cy="12" r="2"/><rect x="9" y="11" width="12" height="2"/>' +
    '<circle class="o" cx="5" cy="18" r="2"/><rect x="9" y="17" width="12" height="2"/>',
  settings: '<circle class="o" cx="12" cy="12" r="8"/><path class="on" d="M12 4a8 8 0 0 1 8 8h-8z"/>'
};

export function icon(name) {
  return '<svg class="sign" viewBox="0 0 24 24" aria-hidden="true">' + (SIGNS[name] || SIGNS.dash) + "</svg>";
}
