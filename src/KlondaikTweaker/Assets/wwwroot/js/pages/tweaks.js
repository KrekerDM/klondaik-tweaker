import { invoke } from "../bridge.js";
import { t } from "../i18n.js";
import { h, esc, toast } from "../ui.js";
import { tweakCard, setCardState } from "../tweakcard.js";
import { runTweaks } from "../actions.js";

const ORDER = [
  "weak", "performance", "gaming", "input", "privacy", "ai", "interface", "services",
  "network", "disk", "power", "updates", "security", "debloat"
];

export default {
  id: "tweaks",
  icon: "tweaks",
  group: "main",

  async render(app) {
    const el = h('<div class="stack" style="gap:20px"></div>');
    const state = { cat: "all", risk: "all", q: "", only: "all" };
    const catalog = new Map();

    el.appendChild(
      h(
        '<div class="page-head"><div><h1>' +
          esc(t("page.tweaks")) +
          '</h1><p class="lead">' +
          esc(t("tw.sub")) +
          "</p></div>" +
          '<div class="row"><input type="search" id="twq" placeholder="' +
          esc(t("tw.search")) +
          '" style="min-width:260px"></div></div>'
      )
    );

    const presetBox = h('<div class="pane stack"><h2>' + esc(t("tw.presets")) + '</h2><div class="row" data-presets></div></div>');
    el.appendChild(presetBox);

    const filters = h('<div class="stack-sm"><div class="row" data-cats></div><div class="row" data-risks></div></div>');
    el.appendChild(filters);

    const listBox = h('<div class="stack" data-list></div>');
    el.appendChild(listBox);

    const bar = h(
      '<div class="sticky-actions">' +
        '<span class="small dim" data-count></span>' +
        '<div class="grow"></div>' +
        '<button class="btn btn-ghost btn-sm" data-sel="all">' + esc(t("act.selectAll")) + "</button>" +
        '<button class="btn btn-ghost btn-sm" data-sel="none">' + esc(t("act.clear")) + "</button>" +
        '<button class="btn btn-ghost" data-bulk="revert">' + esc(t("tw.revertSel")) + "</button>" +
        '<button class="btn btn-primary" data-bulk="apply">' + esc(t("tw.applySel")) + "</button>" +
        "</div>"
    );
    el.appendChild(bar);

    const countEl = bar.querySelector("[data-count]");
    const listEl = listBox;

    function selectedIds() {
      return [...listEl.querySelectorAll(".tw")].filter((x) => x.querySelector(".cb.on")).map((x) => x.dataset.id);
    }

    function updateCount() {
      const total = listEl.querySelectorAll(".tw").length;
      const sel = selectedIds().length;
      countEl.textContent = total + " " + t("tw.total") + " · " + sel + " " + t("tw.selected");
    }

    async function load() {
      listEl.innerHTML = '<div class="skel"></div><div class="skel"></div><div class="skel"></div>';
      const data = await invoke("tweaks.list", { cat: state.cat, risk: state.risk, q: state.q });

      const cats = filters.querySelector("[data-cats]");
      if (!cats.childElementCount) {
        const keys = ORDER.filter((c) => data.cats[c]);
        cats.appendChild(chip("all", t("cat.all"), data.total, state.cat === "all"));
        keys.forEach((c) => cats.appendChild(chip(c, t("cat." + c), data.cats[c], state.cat === c)));
        cats.addEventListener("click", (e) => {
          const c = e.target.closest("[data-chip]");
          if (!c) return;
          state.cat = c.dataset.chip;
          cats.querySelectorAll(".chip").forEach((x) => x.classList.toggle("on", x === c));
          load();
        });

        const risks = filters.querySelector("[data-risks]");
        risks.appendChild(chip("all", t("cat.all"), data.total, true));
        ["safe", "advanced", "extreme"].forEach((r) => {
          if (data.risks[r]) risks.appendChild(chip(r, t("risk." + r), data.risks[r], false));
        });
        risks.addEventListener("click", (e) => {
          const c = e.target.closest("[data-chip]");
          if (!c) return;
          state.risk = c.dataset.chip;
          risks.querySelectorAll(".chip").forEach((x) => x.classList.toggle("on", x === c));
          load();
        });
      }

      listEl.innerHTML = "";
      const showExtreme = app.info.settings.showExtreme;
      const visible = data.items.filter((x) => showExtreme || x.risk !== "extreme");
      if (!visible.length) {
        listEl.appendChild(h('<div class="empty">' + esc(t("msg.empty")) + "</div>"));
        updateCount();
        return;
      }

      const groups = new Map();
      visible.forEach((item) => {
        catalog.set(item.id, item);
        if (!groups.has(item.cat)) groups.set(item.cat, []);
        groups.get(item.cat).push(item);
      });

      [...groups.keys()]
        .sort((a, b) => ORDER.indexOf(a) - ORDER.indexOf(b))
        .forEach((cat) => {
          const items = groups.get(cat);
          const section = h(
            '<section class="stack-sm"><h2 style="margin-top:8px">' +
              esc(t("cat." + cat)) +
              ' <span class="dim mono small">' +
              items.length +
              "</span></h2></section>"
          );
          items.forEach((item) => section.appendChild(tweakCard(item)));
          listEl.appendChild(section);
        });

      updateCount();
    }

    invoke("tweaks.presets").then((presets) => {
      const box = presetBox.querySelector("[data-presets]");
      presets.forEach((p) => {
        const btn = h(
          '<button class="btn btn-ghost" data-preset="' +
            esc(p.id) +
            '" title="' +
            esc(p.desc) +
            '">' +
            esc(p.title) +
            ' <b class="mono dim" style="font-weight:400">' +
            p.count +
            "</b></button>"
        );
        btn.addEventListener("click", async () => {
          const ids = p.ids.filter((id) => app.info.settings.showExtreme || (catalog.get(id) || {}).risk !== "extreme");
          const r = await runTweaks(ids.length ? ids : p.ids, true, catalog);
          if (r) {
            await app.refresh();
            load();
          }
        });
        box.appendChild(btn);
      });
    });

    let debounce = 0;
    el.querySelector("#twq").addEventListener("input", (e) => {
      state.q = e.target.value;
      clearTimeout(debounce);
      debounce = setTimeout(load, 220);
    });

    listEl.addEventListener("click", async (e) => {
      if (e.target.closest("[data-cb]")) {
        updateCount();
        return;
      }
      const sw = e.target.closest("[data-sw]");
      if (!sw) return;
      const card = sw.closest(".tw");
      const id = card.dataset.id;
      const apply = !sw.classList.contains("on");
      sw.classList.add("busy");
      const r = await runTweaks([id], apply, catalog);
      if (r && r.results[0]) {
        setCardState(card, r.results[0].state);
        const item = catalog.get(id);
        if (item) item.state = r.results[0].state;
        await app.refresh();
      } else {
        sw.classList.remove("busy");
      }
    });

    bar.addEventListener("click", async (e) => {
      const sel = e.target.closest("[data-sel]");
      if (sel) {
        const on = sel.dataset.sel === "all";
        listEl.querySelectorAll(".cb").forEach((cb) => cb.classList.toggle("on", on));
        updateCount();
        return;
      }
      const bulk = e.target.closest("[data-bulk]");
      if (!bulk) return;
      const ids = selectedIds();
      if (!ids.length) return toast(t("msg.nothingSelected"), "warn");
      const r = await runTweaks(ids, bulk.dataset.bulk === "apply", catalog);
      if (r) {
        r.results.forEach((res) => {
          const card = listEl.querySelector('.tw[data-id="' + CSS.escape(res.id) + '"]');
          if (card) setCardState(card, res.state);
        });
        await app.refresh();
      }
    });

    await load();
    return { el };
  }
};

function chip(id, label, count, on) {
  return h(
    '<button class="chip' + (on ? " on" : "") + '" data-chip="' + esc(id) + '">' + esc(label) + " <b>" + count + "</b></button>"
  );
}
