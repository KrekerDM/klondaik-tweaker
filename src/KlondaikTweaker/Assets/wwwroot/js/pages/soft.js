import { invoke } from "../bridge.js";
import { t, getLang } from "../i18n.js";
import { h, esc, toast, progress } from "../ui.js";

export default {
  id: "soft",
  icon: "soft",
  group: "extra",

  async render() {
    const el = h('<div class="stack" style="gap:20px"></div>');
    const state = { cat: "all", q: "" };
    let items = [];
    let winget = true;

    el.appendChild(
      h(
        '<div class="page-head"><div><h1>' + esc(t("soft.title")) + '</h1>' +
          '<p class="lead">' + esc(t("soft.sub")) + "</p></div>" +
          '<div class="row"><input type="search" id="softq" placeholder="' + esc(t("tw.search")) + '" style="min-width:220px">' +
          '<button class="btn btn-ghost" data-a="refresh">' + esc(t("act.refresh")) + "</button></div></div>"
      )
    );

    const notice = h('<div class="card hide" data-notice></div>');
    el.appendChild(notice);

    const cats = h('<div class="row" data-cats></div>');
    el.appendChild(cats);

    const grid = h('<div class="grid g3" data-grid></div>');
    el.appendChild(grid);

    function draw() {
      const q = state.q.toLowerCase();
      const list = items.filter((s) => {
        if (state.cat !== "all" && s.cat !== state.cat) return false;
        if (!q) return true;
        const text = (s.name + " " + s.ru + " " + s.en).toLowerCase();
        return text.includes(q);
      });

      grid.innerHTML = "";
      if (!list.length) {
        grid.appendChild(h('<div class="empty">' + esc(t("msg.empty")) + "</div>"));
        return;
      }

      list.forEach((s) => {
        const desc = getLang() === "en" ? s.en : s.ru;
        const card = h(
          '<div class="card stack-sm" data-id="' + esc(s.id) + '">' +
            '<div class="row" style="gap:6px"><h3 class="grow">' + esc(s.name) + "</h3>" +
            (s.pick ? '<span class="tag applied">' + esc(t("soft.pick")) + "</span>" : "") +
            (s.installed ? '<span class="tag safe">' + esc(t("soft.installed")) + "</span>" : "") +
            "</div>" +
            '<p class="small dim" style="min-height:52px">' + esc(desc) + "</p>" +
            '<div class="row"><span class="tag plain">' + esc(s.cat) + "</span><div class=\"grow\"></div>" +
            (s.winget
              ? '<button class="btn ' + (s.installed ? "btn-ghost" : "btn-primary") + ' btn-sm" data-act="' +
                (s.installed ? "uninstall" : "install") + '">' + esc(s.installed ? t("act.uninstall") : t("act.install")) + "</button>"
              : '<button class="btn btn-ghost btn-sm" data-act="site">' + esc(t("soft.site")) + "</button>") +
            "</div></div>"
        );
        grid.appendChild(card);
      });
    }

    grid.addEventListener("click", async (e) => {
      const btn = e.target.closest("[data-act]");
      if (!btn) return;
      const card = btn.closest("[data-id]");
      const id = card.dataset.id;
      const item = items.find((x) => x.id === id);
      if (btn.dataset.act === "site") {
        invoke("sys.link", { url: item.url || "https://klondaik.uk" });
        return;
      }
      if (!winget) return toast(t("soft.noWinget"), "err");
      btn.disabled = true;
      const prev = btn.textContent;
      btn.textContent = "...";
      progress(10);
      try {
        const method = btn.dataset.act === "install" ? "soft.install" : "soft.uninstall";
        const r = await invoke(method, { id }, 1200000);
        progress(100);
        if (r.result === "ok") {
          item.installed = btn.dataset.act === "install";
          toast(item.name + " · " + t("act.finish"));
          draw();
          return;
        }
        toast(item.name + ": " + r.result, "err");
      } catch (err) {
        toast(String(err.message || err), "err");
      } finally {
        progress(100);
        btn.disabled = false;
        btn.textContent = prev;
      }
    });

    el.addEventListener("click", async (e) => {
      const btn = e.target.closest('[data-a="refresh"]');
      if (!btn) return;
      btn.disabled = true;
      try {
        await load(true);
      } finally {
        btn.disabled = false;
      }
    });

    let debounce = 0;
    el.querySelector("#softq").addEventListener("input", (e) => {
      state.q = e.target.value;
      clearTimeout(debounce);
      debounce = setTimeout(draw, 180);
    });

    async function load(refresh) {
      grid.innerHTML = '<div class="skel"></div><div class="skel"></div><div class="skel"></div>';
      const data = await invoke("soft.list", {}, 60000);
      items = data.items;
      winget = data.winget;
      notice.classList.toggle("hide", winget);
      if (!winget) notice.textContent = t("soft.noWinget");

      if (!data.ready || refresh) {
        invoke("soft.state", { refresh: !!refresh }, 300000)
          .then((state) => {
            items = state.items;
            winget = state.winget;
            notice.classList.toggle("hide", winget);
            if (!winget) notice.textContent = t("soft.noWinget");
            draw();
          })
          .catch(() => {});
      }

      if (!cats.childElementCount) {
        const keys = [...new Set(items.map((x) => x.cat))].sort();
        const add = (id, label) => {
          const chip = h('<button class="chip' + (id === "all" ? " on" : "") + '" data-c="' + esc(id) + '">' + esc(label) + "</button>");
          cats.appendChild(chip);
        };
        add("all", t("cat.all"));
        keys.forEach((k) => add(k, k));
        cats.addEventListener("click", (e) => {
          const chip = e.target.closest("[data-c]");
          if (!chip) return;
          state.cat = chip.dataset.c;
          cats.querySelectorAll(".chip").forEach((x) => x.classList.toggle("on", x === chip));
          draw();
        });
      }
      draw();
    }

    await load(false);
    return { el };
  }
};
