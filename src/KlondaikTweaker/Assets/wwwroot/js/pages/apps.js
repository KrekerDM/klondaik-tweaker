import { invoke } from "../bridge.js";
import { t } from "../i18n.js";
import { h, esc, toast, confirmBox, progress } from "../ui.js";

const GROUPS = ["xbox", "bing", "ai", "social", "media", "microsoft", "other", "essential"];

export default {
  id: "apps",
  icon: "apps",
  group: "system",

  async render() {
    const el = h('<div class="stack" style="gap:20px"></div>');
    el.appendChild(
      h(
        '<div class="page-head"><div><h1>' + esc(t("apps.title")) + '</h1>' +
          '<p class="lead">' + esc(t("apps.sub")) + "</p></div>" +
          '<button class="btn btn-ghost" data-a="refresh">' + esc(t("act.refresh")) + "</button></div>"
      )
    );

    const body = h('<div class="stack"></div>');
    el.appendChild(body);

    const bar = h(
      '<div class="sticky-actions"><span class="small dim" data-count></span><div class="grow"></div>' +
        '<button class="btn btn-ghost btn-sm" data-sel="none">' + esc(t("act.clear")) + "</button>" +
        '<button class="btn btn-danger" data-a="remove">' + esc(t("apps.removeSel")) + "</button></div>"
    );
    el.appendChild(bar);

    function selected() {
      return [...body.querySelectorAll(".item")].filter((x) => x.querySelector(".cb.on")).map((x) => x.dataset.name);
    }

    function updateCount() {
      bar.querySelector("[data-count]").textContent = selected().length + " " + t("tw.selected");
    }

    async function load(refresh) {
      body.innerHTML = '<div class="skel"></div><div class="skel"></div>';
      const items = await invoke("appx.list", { refresh: !!refresh });
      body.innerHTML = "";
      const byGroup = new Map();
      items.forEach((a) => {
        if (a.framework) return;
        const g = a.group || "other";
        if (!byGroup.has(g)) byGroup.set(g, []);
        byGroup.get(g).push(a);
      });

      GROUPS.forEach((g) => {
        const list = byGroup.get(g);
        if (!list || !list.length) return;
        const section = h(
          '<section class="stack-sm"><h2>' + esc(t("apps.group." + g)) +
            ' <span class="dim small mono">' + list.length + "</span></h2><div class=\"list\"></div></section>"
        );
        const box = section.querySelector(".list");
        list.forEach((a) => {
          const locked = g === "essential";
          const row = h(
            '<div class="item" data-name="' + esc(a.name) + '">' +
              (locked ? '<div style="width:16px"></div>' : '<div class="cb"></div>') +
              '<div class="grow" style="min-width:0"><div class="name">' + esc(a.display) + "</div>" +
              '<div class="sub mono">' + esc(a.name) + "</div></div>" +
              (a.system ? '<span class="tag plain">system</span>' : "") +
              (locked ? '<span class="tag safe">' + esc(t("apps.essential")) + "</span>" : "") +
              "</div>"
          );
          const cb = row.querySelector(".cb");
          if (cb)
            cb.addEventListener("click", () => {
              cb.classList.toggle("on");
              updateCount();
            });
          box.appendChild(row);
        });
        body.appendChild(section);
      });

      if (!byGroup.size) body.appendChild(h('<div class="empty">' + esc(t("msg.empty")) + "</div>"));
      updateCount();
    }

    el.addEventListener("click", async (e) => {
      const btn = e.target.closest("[data-a], [data-sel]");
      if (!btn) return;
      if (btn.dataset.sel === "none") {
        body.querySelectorAll(".cb").forEach((c) => c.classList.remove("on"));
        updateCount();
        return;
      }
      if (btn.dataset.a === "refresh") return load(true);
      if (btn.dataset.a !== "remove") return;

      const names = selected();
      if (!names.length) return toast(t("msg.nothingSelected"), "warn");
      const ok = await confirmBox(t("apps.removeSel"), names.join("\n"), t("act.remove"), true);
      if (!ok) return;

      btn.disabled = true;
      progress(5);
      try {
        const res = await invoke("appx.remove", { names });
        progress(100);
        const failed = res.filter((x) => x.result !== "ok" && x.result !== "not installed");
        toast(t("act.remove") + ": " + (res.length - failed.length) + (failed.length ? " · " + t("msg.failed") + ": " + failed.length : ""), failed.length ? "err" : "");
        await load(true);
      } catch (err) {
        toast(String(err.message || err), "err");
      } finally {
        btn.disabled = false;
      }
    });

    await load(false);
    return { el };
  }
};
