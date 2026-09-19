import { invoke, on } from "../bridge.js";
import { t, getLang } from "../i18n.js";
import { h, esc, toast, switchEl, progress, bytes } from "../ui.js";
import { setEnabled } from "../scene.js";
import { restartExplorer } from "../actions.js";

export default {
  id: "settings",
  icon: "settings",
  group: "extra",

  async render(app) {
    const s = app.info.settings;
    const el = h('<div class="stack" style="gap:20px;max-width:860px"></div>');

    el.appendChild(h('<div><h1>' + esc(t("set.title")) + "</h1></div>"));

    const opts = h(
      '<div class="pane stack">' +
        '<div class="list">' +
        row("lang", t("set.lang"), "", true) +
        row("showExtreme", t("set.extreme"), t("set.extremeHint")) +
        row("autoRestorePoint", t("set.restore"), t("set.restoreHint")) +
        row("monitor3d", t("set.3d"), t("set.3dHint")) +
        row("liveMonitor", t("set.live"), t("set.liveHint")) +
        row("reduced", t("set.reduced"), t("set.reducedHint")) +
        "</div></div>"
    );
    el.appendChild(opts);

    const langBox = opts.querySelector('[data-row="lang"] [data-ctl]');
    langBox.appendChild(
      h(
        '<select data-lang><option value="ru"' + (s.lang === "ru" ? " selected" : "") + ">Русский</option>" +
          '<option value="en"' + (s.lang === "en" ? " selected" : "") + ">English</option></select>"
      )
    );
    langBox.querySelector("[data-lang]").addEventListener("change", async (e) => {
      await invoke("app.setSetting", { key: "lang", value: e.target.value });
      await app.reload();
    });

    const toggles = [
      ["showExtreme", s.showExtreme],
      ["autoRestorePoint", s.autoRestorePoint],
      ["monitor3d", s.monitor3d],
      ["liveMonitor", s.liveMonitor],
      ["reduced", s.reduced]
    ];
    toggles.forEach(([key, value]) => {
      const box = opts.querySelector('[data-row="' + key + '"] [data-ctl]');
      box.appendChild(
        switchEl(value, async (next) => {
          const updated = await invoke("app.setSetting", { key, value: next });
          app.info.settings = updated;
          if (key === "monitor3d") setEnabled(next);
          if (key === "reduced") document.body.classList.toggle("reduced", next);
          return true;
        })
      );
    });

    const tools = h(
      '<div class="pane stack"><h2>' + esc(t("nav.extra")) + "</h2>" +
        '<div class="row">' +
        '<button class="btn btn-ghost" data-a="data">' + esc(t("set.openData")) + "</button>" +
        '<button class="btn btn-ghost" data-a="explorer">' + esc(t("msg.restartExplorer")) + "</button>" +
        '<button class="btn btn-ghost" data-a="rstrui">' + esc(t("set.systemRestore")) + "</button>" +
        '<button class="btn btn-ghost" data-a="site">' + esc(t("set.site")) + "</button>" +
        "</div>" +
        '<p class="small dim mono">' + esc(app.info.dataDir) + "</p></div>"
    );
    el.appendChild(tools);

    const upd = h(
      '<div class="pane stack"><h2>' + esc(t("upd.title")) + "</h2>" +
        '<div class="stack-sm">' +
        '<div class="name" data-state>' + esc(t("upd.current")) + " " + esc(app.info.version) + "</div>" +
        '<div class="sub" data-sub>' + esc(t("upd.hint")) + "</div></div>" +
        '<div class="small dim" data-notes hidden style="white-space:pre-wrap;max-height:220px;overflow:auto"></div>' +
        '<div class="row">' +
        '<button class="btn btn-primary btn-sm" data-u="install" hidden>' + esc(t("upd.install")) + "</button>" +
        '<button class="btn btn-ghost btn-sm" data-u="check">' + esc(t("upd.check")) + "</button>" +
        '<button class="btn btn-ghost btn-sm" data-u="releases">' + esc(t("upd.releases")) + "</button>" +
        "</div></div>"
    );
    el.appendChild(upd);

    const stateEl = upd.querySelector("[data-state]");
    const subEl = upd.querySelector("[data-sub]");
    const notesEl = upd.querySelector("[data-notes]");
    const checkBtn = upd.querySelector('[data-u="check"]');
    const installBtn = upd.querySelector('[data-u="install"]');
    let found = null;
    let busy = false;

    const off = on("update", (data) => {
      if (!data) return;
      if (data.stage === "download") {
        progress(data.percent || 0);
        subEl.textContent = t("upd.downloading") + " " + (data.percent || 0) + "%";
      } else if (data.stage === "install") {
        progress(100);
        subEl.textContent = t("upd.installing");
      }
    });

    upd.addEventListener("click", async (e) => {
      const btn = e.target.closest("[data-u]");
      if (!btn || busy) return;
      const action = btn.dataset.u;

      if (action === "releases") {
        await invoke("sys.link", { url: "https://github.com/Faliseven/klondaik-tweaker/releases" });
        return;
      }

      if (action === "check") {
        busy = true;
        checkBtn.disabled = true;
        stateEl.textContent = t("upd.checking");
        try {
          const info = await invoke("update.check");
          if (info.error) {
            stateEl.textContent = t("upd.failed");
            subEl.textContent = info.error;
          } else if (info.available) {
            found = info;
            stateEl.textContent = t("upd.found") + " " + info.latest;
            subEl.textContent = t("upd.current") + " " + info.current + (info.size ? " · " + bytes(info.size) : "");
            installBtn.hidden = false;
            if (info.notes) {
              notesEl.textContent = info.notes;
              notesEl.hidden = false;
            }
          } else {
            stateEl.textContent = t("upd.latest");
            subEl.textContent = t("upd.current") + " " + info.current;
          }
        } catch (err) {
          stateEl.textContent = t("upd.failed");
          subEl.textContent = err.message;
        }
        checkBtn.disabled = false;
        busy = false;
        return;
      }

      if (action === "install" && found) {
        busy = true;
        installBtn.disabled = true;
        checkBtn.disabled = true;
        try {
          const result = await invoke("update.install", { url: found.url });
          if (!result.ok) {
            progress(0);
            toast(t("upd.error") + ": " + (result.message || ""), "err");
            installBtn.disabled = false;
            checkBtn.disabled = false;
            busy = false;
          }
        } catch (err) {
          progress(0);
          toast(t("upd.error") + ": " + err.message, "err");
          installBtn.disabled = false;
          checkBtn.disabled = false;
          busy = false;
        }
      }
    });

    const about = h(
      '<div class="pane stack"><h2>' + esc(t("set.about")) + "</h2>" +
        '<p class="lead small">' + esc(t("set.aboutText")) + "</p>" +
        '<div class="spec">' +
        "<div><dt>Version</dt><dd>" + esc(app.info.version) + "</dd></div>" +
        "<div><dt>Tweaks</dt><dd>" + app.info.tweakCount + "</dd></div>" +
        "<div><dt>Journal</dt><dd>" + app.info.journal + "</dd></div>" +
        "<div><dt>Site</dt><dd>klondaik.uk</dd></div>" +
        "</div></div>"
    );
    el.appendChild(about);

    const credits = h(
      '<div class="pane stack"><h2>' + esc(t("set.credits")) + "</h2>" +
        '<p class="small dim" data-note></p>' +
        '<div class="list" data-sources></div></div>'
    );
    el.appendChild(credits);

    invoke("app.credits")
      .then((data) => {
        const lang = getLang();
        credits.querySelector("[data-note]").textContent = (data.note && data.note[lang]) || "";
        const box = credits.querySelector("[data-sources]");
        box.innerHTML = "";
        (data.sources || []).forEach((s) => {
          const row = h(
            '<div class="item"><div class="grow" style="min-width:0">' +
              '<div class="name">' + esc(s.name) + ' <span class="tag plain">' + esc(s.license) + "</span></div>" +
              '<div class="sub">' + esc(s.authors) + "</div>" +
              '<div class="sub">' + esc(s[lang] || s.ru || "") + "</div></div>" +
              '<button class="btn btn-ghost btn-sm" data-url="' + esc(s.url) + '">' + esc(t("act.open")) + "</button></div>"
          );
          box.appendChild(row);
        });
      })
      .catch(() => {});

    credits.addEventListener("click", (e) => {
      const link = e.target.closest("[data-url]");
      if (link) invoke("sys.link", { url: link.dataset.url });
    });

    el.addEventListener("click", async (e) => {
      const btn = e.target.closest("[data-a]");
      if (!btn) return;
      const action = btn.dataset.a;
      if (action === "data") await invoke("sys.folder");
      else if (action === "explorer") await restartExplorer();
      else if (action === "rstrui") await invoke("restore.open");
      else if (action === "site") await invoke("sys.link", { url: "https://klondaik.uk" });
    });

    return { el, dispose: off };
  }
};

function row(key, title, hint, select) {
  return (
    '<div class="item" data-row="' + esc(key) + '">' +
    '<div class="grow"><div class="name">' + esc(title) + "</div>" +
    (hint ? '<div class="sub">' + esc(hint) + "</div>" : "") +
    "</div>" +
    '<div data-ctl></div></div>'
  );
}
