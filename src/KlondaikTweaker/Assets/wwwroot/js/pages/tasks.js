import { invoke } from "../bridge.js";
import { t } from "../i18n.js";
import { h, esc, toast, switchEl } from "../ui.js";

export default {
  id: "tasks",
  icon: "tasks",
  group: "system",

  async render() {
    const el = h('<div class="stack" style="gap:20px;max-width:980px"></div>');

    el.appendChild(
      h(
        '<div class="page-head"><div><h1>' + esc(t("task.title")) + "</h1>" +
          '<p class="lead">' + esc(t("task.sub")) + "</p></div>" +
          '<div class="row"><button class="btn btn-ghost btn-sm" data-a="refresh">' + esc(t("act.refresh")) + "</button></div></div>"
      )
    );

    const body = h('<div class="stack"></div>');
    el.appendChild(body);

    async function load() {
      body.innerHTML = '<div class="stack"><div class="skel"></div><div class="skel"></div></div>';
      let groups;
      try {
        groups = await invoke("tasks.list", {}, 300000);
      } catch (err) {
        body.innerHTML = "";
        body.appendChild(h('<div class="pane"><p class="small dim mono">' + esc(String(err.message || err)) + "</p></div>"));
        return;
      }

      if (!groups.length) {
        body.innerHTML = "";
        body.appendChild(h('<div class="empty">' + esc(t("task.none")) + "</div>"));
        return;
      }

      body.innerHTML = "";
      groups.forEach((g) => {
        const allOn = g.enabled === g.tasks.length;
        const tag = g.rec === "off"
          ? '<span class="tag safe">' + esc(t("feat.canOff")) + "</span>"
          : '<span class="tag plain">' + esc(t("task.keep")) + "</span>";

        const pane = h(
          '<div class="pane stack">' +
            '<div class="item" style="padding:0;border:0;background:none">' +
            '<div class="grow" style="min-width:0">' +
            "<h2>" + esc(g.title) + " " + tag + "</h2>" +
            '<p class="small dim">' + esc(g.desc) + "</p>" +
            '<p class="small dim">' + esc(t("task.count")) + ": " + g.tasks.length +
            " · " + esc(t("task.enabled")) + ": " + g.enabled +
            (g.missing ? " · " + esc(t("task.missing")) + ": " + g.missing : "") + "</p>" +
            "</div><div data-group></div></div>" +
            '<div class="list" data-rows></div></div>'
        );

        const rows = pane.querySelector("[data-rows]");
        const redraw = () => {
          rows.innerHTML = "";
          g.tasks.forEach((task) => {
            const row = h(
              '<div class="item">' +
                '<div class="grow" style="min-width:0">' +
                '<div class="name mono">' + esc(task.name) + "</div>" +
                '<div class="sub mono dim">' + esc(task.path) + "</div>" +
                "</div><div data-one></div></div>"
            );
            row.querySelector("[data-one]").appendChild(
              switchEl(task.enabled, async (next) => {
                const r = await invoke("tasks.setOne", { path: task.path, enable: next }, 120000);
                if (r.ok === false) {
                  toast(task.name + ": " + (r.message || t("msg.failed")), "err");
                  return false;
                }
                task.enabled = next;
                g.enabled = g.tasks.filter((x) => x.enabled).length;
                return true;
              })
            );
            rows.appendChild(row);
          });
        };
        redraw();

        pane.querySelector("[data-group]").appendChild(
          switchEl(allOn, async (next) => {
            const r = await invoke("tasks.setGroup", { id: g.id, enable: next }, 300000);
            if (r.ok === false) {
              toast(g.title + ": " + (r.message || t("msg.failed")), "err");
              return false;
            }
            g.tasks.forEach((x) => (x.enabled = next));
            g.enabled = next ? g.tasks.length : 0;
            redraw();
            toast(g.title + ": " + (r.message || t("act.finish")));
            return true;
          })
        );

        body.appendChild(pane);
      });
    }

    el.addEventListener("click", (e) => {
      if (e.target.closest('[data-a="refresh"]')) load();
    });

    await load();
    return { el };
  }
};
