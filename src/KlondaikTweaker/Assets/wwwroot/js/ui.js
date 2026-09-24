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
  if (n < 1024) return n + " B";
  if (n < 1024 * 1024) return (n / 1024).toFixed(0) + " KB";
  if (n < 1024 * 1024 * 1024) return (n / 1024 / 1024).toFixed(n < 10 * 1024 * 1024 ? 1 : 0) + " " + t("unit.mb");
  return (n / 1024 / 1024 / 1024).toFixed(2) + " " + t("unit.gb");
}

export function num(value, digits = 0) {
  return (Number(value) || 0).toLocaleString(getLang() === "en" ? "en-US" : "ru-RU", {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits
  });
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

export function icon(name) {
  const paths = {
    dash: "M3 3h7v7H3zM14 3h7v4h-7zM14 10h7v11h-7zM3 13h7v8H3z",
    wizard: "M12 3l2.2 5.4L20 9.8l-4 4 1 5.8-5-2.9-5 2.9 1-5.8-4-4 5.8-1.4z",
    tweaks: "M4 7h10M18 7h2M4 17h4M12 17h8M15 4v6M8 14v6",
    clean: "M4 8h16l-1.4 12H5.4zM9 8V4h6v4",
    tools: "M14.7 6.3a4 4 0 01-5.4 5.4L4 17v3h3l5.3-5.3a4 4 0 015.4-5.4l-2.8 2.8-2.4-.6-.6-2.4z",
    startup: "M12 3v12M7 8l5-5 5 5M4 18v3h16v-3",
    power: "M12 3v9M7.5 6.5a7 7 0 1 0 9 0",
    nic: "M4 7h16v10H4zM8 17v3M16 17v3M7 11h2M11 11h2M15 11h2",
    irq: "M9 3v3M15 3v3M9 18v3M15 18v3M3 9h3M3 15h3M18 9h3M18 15h3M7 7h10v10H7z",
    tasks: "M4 5h4v4H4zM10 6h10M4 11h4v4H4zM10 12h10M4 17h4v4H4zM10 18h10",
    features: "M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z",
    services: "M12 8a4 4 0 100 8 4 4 0 000-8zM12 2v3M12 19v3M4.2 4.2l2.1 2.1M17.7 17.7l2.1 2.1M2 12h3M19 12h3M4.2 19.8l2.1-2.1M17.7 6.3l2.1-2.1",
    apps: "M3 3h8v8H3zM13 3h8v8h-8zM3 13h8v8H3zM13 13h8v8h-8z",
    network: "M12 3a9 9 0 100 18 9 9 0 000-18zM3 12h18M12 3c3 3.5 3 14.5 0 18M12 3c-3 3.5-3 14.5 0 18",
    bench: "M12 20V10M6 20v-6M18 20V4M3 20h18",
    soft: "M21 8l-9-5-9 5 9 5zM3 12l9 5 9-5M3 16l9 5 9-5",
    journal: "M5 3h11l4 4v14H5zM16 3v4h4M8 12h8M8 16h5",
    settings: "M12 9a3 3 0 100 6 3 3 0 000-6zM19.4 13.5l1.6 1.2-2 3.4-1.9-.7a7 7 0 01-1.8 1l-.3 2h-4l-.3-2a7 7 0 01-1.8-1l-1.9.7-2-3.4 1.6-1.2a7 7 0 010-2l-1.6-1.2 2-3.4 1.9.7a7 7 0 011.8-1l.3-2h4l.3 2a7 7 0 011.8 1l1.9-.7 2 3.4-1.6 1.2a7 7 0 010 2z"
  };
  const d = paths[name] || paths.dash;
  return (
    '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="square"><path d="' +
    d +
    '"/></svg>'
  );
}
