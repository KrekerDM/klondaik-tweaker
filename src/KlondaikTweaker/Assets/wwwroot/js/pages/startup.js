import { invoke } from "../bridge.js";
import { t } from "../i18n.js";
import { h, esc, toast, switchEl, confirmBox } from "../ui.js";

export default {
  id: "startup",
  icon: "startup",
  group: "system",

  async render() {
    const el = h('<div class="stack" style="gap:20px"></div>');
    el.appendChild(
      h(
        '<div class="page-head"><div><h1>' + esc(t("start.title")) + '</h1>' +
          '<p class="lead">' + esc(t("start.sub")) + "</p></div>" +
          '<button class="btn btn-ghost" data-a="refresh">' + esc(t("act.refresh")) + "</button></div>"
      )
    );

    const body = h('<div class="stack"></div>');
    el.appendChild(body);

    async function load() {
      body.innerHTML = '<div class="skel"></div><div class="skel"></div>';
      const items = await invoke("startup.list");
      body.innerHTML = "";

      const groups = [
        { key: "reg", title: t("start.reg"), items: items.filter((x) => x.kind !== "task") },
        { key: "task", title: t("start.tasks"), items: items.filter((x) => x.kind === "task") }
      ];

      groups.forEach((g) => {
        if (!g.items.length) return;
        const on = g.items.filter((x) => x.enabled).length;
        const section = h(
          '<section class="stack-sm"><h2>' + esc(g.title) +
            ' <span class="dim small mono">' + on + " / " + g.items.length + " " + esc(t("start.enabled")) + "</span></h2>" +
            '<div class="list"></div></section>'
        );
        const list = section.querySelector(".list");
        g.items.forEach((item) => {
          const row = h(
            '<div class="item">' +
              '<div class="grow" style="min-width:0"><div class="name">' + esc(item.name) + "</div>" +
              '<div class="sub mono">' + esc(item.command || "") + "</div></div>" +
              '<span class="tag plain">' + esc(item.source) + "</span>" +
              '<div data-sw></div>' +
              '<button class="btn btn-ghost btn-sm" data-del title="' + esc(t("act.remove")) + '">×</button>' +
              "</div>"
          );
          row.querySelector("[data-sw]").appendChild(
            switchEl(item.enabled, async (next) => {
              const r = await invoke("startup.toggle", { id: item.id, enabled: next });
              if (!r.ok) toast(t("msg.failed") + ": " + item.name, "err");
              return r.ok;
            })
          );
          row.querySelector("[data-del]").addEventListener("click", async () => {
            const ok = await confirmBox(t("act.remove"), item.name + "\n" + (item.command || ""), t("act.remove"), true);
            if (!ok) return;
            const r = await invoke("startup.delete", { id: item.id });
            if (r.ok) {
              row.remove();
              toast(t("act.remove") + ": " + item.name);
            } else toast(t("msg.failed"), "err");
          });
          list.appendChild(row);
        });
        body.appendChild(section);
      });

      if (!items.length) body.appendChild(h('<div class="empty">' + esc(t("msg.empty")) + "</div>"));
    }

    el.querySelector('[data-a="refresh"]').addEventListener("click", load);
    await load();
    return { el };
  }
};
