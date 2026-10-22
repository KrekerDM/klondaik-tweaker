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

const TOOL_SIGNS = {
  services:
    '<rect class="on" x="8" y="9" width="32" height="7"/>' +
    '<rect class="o" x="8" y="20" width="32" height="7"/>' +
    '<rect class="o" x="8" y="31" width="32" height="7"/>',
  defender:
    '<path class="o" d="M24 5 L40 11 V25 C40 33 33 39 24 43 C15 39 8 33 8 25 V11 Z"/>' +
    '<path class="on" d="M17 23 l5 5 l10 -11"/>',
  updates:
    '<path class="o" d="M24 10 A14 14 0 1 0 38 24"/>' +
    '<path class="on" d="M17 4 L29 10 L17 16 Z"/>',
  store:
    '<rect class="o" x="9" y="18" width="30" height="22"/>' +
    '<path class="on" d="M17 18 V14 a7 7 0 0 1 14 0 V18"/>',
  search:
    '<circle class="o" cx="21" cy="21" r="11"/>' +
    '<path class="on" d="M29 29 L41 41"/>',
  network:
    '<path class="o" d="M24 24 L24 11 M24 24 L36 32 M24 24 L12 32"/>' +
    '<circle class="o" cx="24" cy="9" r="5"/>' +
    '<circle class="o" cx="38" cy="33" r="5"/>' +
    '<circle class="o" cx="10" cy="33" r="5"/>' +
    '<circle class="on" cx="24" cy="24" r="5"/>',
  memory:
    '<rect class="o" x="6" y="14" width="36" height="18"/>' +
    '<rect class="on" x="11" y="19" width="7" height="8"/>' +
    '<rect class="on" x="21" y="19" width="7" height="8"/>' +
    '<rect class="on" x="31" y="19" width="7" height="8"/>' +
    '<rect x="11" y="35" width="5" height="5"/><rect x="21" y="35" width="5" height="5"/>' +
    '<rect x="31" y="35" width="5" height="5"/>',
  dns:
    '<rect x="22" y="8" width="4" height="34"/>' +
    '<path class="on" d="M26 11 H40 l4 5 l-4 5 H26 Z"/>' +
    '<path class="o" d="M22 25 H8 l-4 5 l4 5 H22 Z"/>',
  godmode:
    '<circle class="on o" cx="24" cy="13" r="8"/>' +
    '<rect x="22" y="20" width="4" height="22"/>' +
    '<rect x="26" y="28" width="8" height="4"/>' +
    '<rect x="26" y="36" width="6" height="4"/>',
  ctxti:
    '<rect class="o" x="5" y="10" width="38" height="28"/>' +
    '<rect x="5" y="10" width="38" height="6"/>' +
    '<path class="on" d="M13 22 l6 5 l-6 5"/>' +
    '<rect class="on" x="23" y="29" width="12" height="3"/>',
  ctxown:
    '<path class="o" d="M16 21 V15 a8 8 0 0 1 16 0 V21"/>' +
    '<rect class="o" x="10" y="21" width="28" height="20"/>' +
    '<circle class="on" cx="24" cy="29" r="3"/>' +
    '<rect class="on" x="23" y="31" width="2" height="6"/>',
  explorer:
    '<path class="o" d="M6 13 H20 l4 5 H42 V39 H6 Z"/>' +
    '<rect class="on" x="6" y="9" width="14" height="5"/>'
};

export function toolSign(name) {
  return (
    '<svg class="sign sign-lg" viewBox="0 0 48 48" aria-hidden="true">' +
    (TOOL_SIGNS[name] || TOOL_SIGNS.services) +
    "</svg>"
  );
}
