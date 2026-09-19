import { invoke, send, on } from "./bridge.js";
import { setLang, getLang, t } from "./i18n.js";
import { h, esc, toast, icon, progress, modal } from "./ui.js";
import { initScene, setEnabled } from "./scene.js";

import dash from "./pages/dash.js";
import wizard from "./pages/wizard.js";
import tweaks from "./pages/tweaks.js";
import clean from "./pages/clean.js";
import tools from "./pages/tools.js";
import startup from "./pages/startup.js";
import services from "./pages/services.js";
import apps from "./pages/apps.js";
import network from "./pages/network.js";
import bench from "./pages/bench.js";
import soft from "./pages/soft.js";
import journal from "./pages/journal.js";
import settings from "./pages/settings.js";

const PAGES = [dash, wizard, tweaks, tools, clean, startup, services, apps, network, bench, soft, journal, settings];
const GROUPS = ["main", "system", "extra"];

const app = {
  info: null,
  current: null,
  active: null,

  async go(id) {
    const page = PAGES.find((p) => p.id === id) || PAGES[0];
    if (this.active && this.active.dispose) {
      try {
        this.active.dispose();
      } catch {}
    }
    this.active = null;
    this.current = page.id;
    document.querySelectorAll(".navitem").forEach((n) => n.classList.toggle("on", n.dataset.page === page.id));
    const content = document.getElementById("content");
    content.innerHTML = '<div class="stack"><div class="skel"></div><div class="skel"></div></div>';
    try {
      const result = await page.render(app);
      content.innerHTML = "";
      content.appendChild(result.el);
      content.scrollTop = 0;
      this.active = result;
    } catch (err) {
      content.innerHTML = "";
      content.appendChild(
        h('<div class="pane stack"><h2>' + esc(t("msg.failed")) + '</h2><p class="small dim mono">' + esc(String(err.message || err)) + "</p></div>")
      );
      console.error(err);
    }
  },

  async refresh() {
    try {
      app.info = await invoke("app.info");
      buildNav();
    } catch {}
  },

  async reload() {
    app.info = await invoke("app.info");
    setLang(app.info.settings.lang);
    document.getElementById("langBtn").textContent = getLang().toUpperCase();
    buildNav();
    await app.go(app.current || "dash");
  }
};

function buildNav() {
  const nav = document.getElementById("nav");
  const scroll = nav.scrollTop;
  nav.innerHTML = "";
  GROUPS.forEach((group) => {
    const pages = PAGES.filter((p) => p.group === group);
    if (!pages.length) return;
    nav.appendChild(h('<div class="navgroup">' + esc(t("nav." + group)) + "</div>"));
    pages.forEach((p) => {
      let badge = "";
      if (p.id === "tweaks" && app.info) badge = String(app.info.tweakCount);
      if (p.id === "journal" && app.info) badge = String(app.info.journal);
      const item = h(
        '<button class="navitem' + (app.current === p.id ? " on" : "") + '" data-page="' + p.id + '" title="' + esc(t("page." + p.id)) + '">' +
          icon(p.icon) +
          "<span>" + esc(t("page." + p.id)) + "</span>" +
          (badge && badge !== "0" ? '<span class="badge">' + badge + "</span>" : "") +
          "</button>"
      );
      item.addEventListener("click", () => app.go(p.id));
      nav.appendChild(item);
    });
  });

  const foot = h('<div class="rail-foot"><div class="small dim">klondaik.uk</div></div>');
  foot.addEventListener("click", () => invoke("sys.link", { url: "https://klondaik.uk" }));
  nav.appendChild(foot);
  nav.scrollTop = scroll;
}

function wireChrome() {
  const winMap = { min: "window.minimize", max: "window.maximize", close: "window.close" };
  document.querySelectorAll("[data-win]").forEach((btn) => {
    btn.addEventListener("click", () => send(winMap[btn.dataset.win]));
  });

  const drag = document.getElementById("drag");
  drag.addEventListener("mousedown", (e) => {
    if (e.button !== 0) return;
    send("window.drag");
  });
  drag.addEventListener("dblclick", () => send("window.maximize"));

  document.getElementById("langBtn").addEventListener("click", async () => {
    const next = getLang() === "ru" ? "en" : "ru";
    await invoke("app.setSetting", { key: "lang", value: next });
    await app.reload();
  });

  window.addEventListener("keydown", (e) => {
    if (e.key === "F5" || (e.ctrlKey && e.key.toLowerCase() === "r")) {
      e.preventDefault();
      app.go(app.current);
    }
  });
}

async function askRisk() {
  const body =
    '<p class="lead small">' + esc(t("risk.p1")) + "</p>" +
    '<p class="lead small">' + esc(t("risk.p2")) + "</p>" +
    '<p class="small dim">' + esc(t("risk.p3")) + "</p>";
  for (;;) {
    const answer = await modal({
      title: t("risk.title"),
      body,
      actions: [
        { id: "ok", label: t("risk.accept"), primary: true },
        { id: "no", label: t("risk.exit") }
      ]
    });
    if (answer === "ok") return true;
    if (answer === "no") return false;
  }
}

async function boot() {
  try {
    app.info = await invoke("app.info");
  } catch (err) {
    document.getElementById("content").innerHTML =
      '<div class="pane"><h2>bridge error</h2><p class="small mono">' + esc(String(err.message || err)) + "</p></div>";
    return;
  }

  setLang(app.info.settings.lang);
  document.getElementById("langBtn").textContent = getLang().toUpperCase();
  document.getElementById("verTag").textContent = "v" + app.info.version;
  document.body.classList.toggle("reduced", !!app.info.settings.reduced);

  if (app.info.settings.monitor3d) {
    const ok = initScene(document.getElementById("bg"));
    if (!ok) setEnabled(false);
  } else {
    setEnabled(false);
  }

  wireChrome();
  buildNav();

  on("window", (data) => {
    document.body.classList.toggle("maximized", !!data.max);
  });

  if (!app.info.settings.acceptedRisk) {
    const accepted = await askRisk();
    if (!accepted) {
      await invoke("app.quit");
      return;
    }
    await invoke("app.setSetting", { key: "acceptedRisk", value: true });
    app.info.settings.acceptedRisk = true;
  }

  await app.go(app.info.settings.wizardDone ? "dash" : "wizard");

  if (app.info.facts.tamperProtection) {
    setTimeout(() => toast(t("msg.tamper"), "warn"), 1200);
  }
  progress(0);
}

boot();
