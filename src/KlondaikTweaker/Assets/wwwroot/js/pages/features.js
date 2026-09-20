import { invoke } from "../bridge.js";
import { t } from "../i18n.js";
import { h, esc, toast, confirmBox, switchEl } from "../ui.js";

const GROUPS = ["legacy", "print", "media", "corp", "server", "virt", "runtime", "system", "other"];

export default {
  id: "features",
  icon: "features",
  group: "system",

  async render() {
    const el = h('<div class="stack" style="gap:20px;max-width:980px"></div>');

    el.appendChild(
      h(
        '<div class="page-head"><div><h1>' + esc(t("feat.title")) + "</h1>" +
          '<p class="lead">' + esc(t("feat.sub")) + "</p></div>" +
          '<div class="row"><button class="btn btn-ghost btn-sm" data-a="refresh">' + esc(t("act.refresh")) + "</button></div></div>"
      )
    );

    const body = h('<div class="stack"></div>');
    el.appendChild(body);

    async function load(refresh) {
      body.innerHTML = '<div class="stack"><div class="skel"></div><div class="skel"></div></div>';
      let items;
      try {
        items = await invoke("features.list", { refresh: !!refresh }, 300000);
      } catch (err) {
        body.innerHTML = "";
        body.appendChild(h('<div class="pane"><p class="small dim mono">' + esc(String(err.message || err)) + "</p></div>"));
        return;
      }

      if (!items.length) {
        body.innerHTML = "";
        body.appendChild(h('<div class="empty">' + esc(t("feat.none")) + "</div>"));
        return;
      }

      body.innerHTML = "";
      GROUPS.forEach((g) => {
        const group = items.filter((x) => x.group === g);
        if (!group.length) return;

        const pane = h(
          '<div class="pane stack"><h2>' + esc(t("feat.group." + g)) + ' <span class="dim">' + group.length + "</span></h2>" +
            '<div class="list"></div></div>'
        );
        const list = pane.querySelector(".list");

        group.forEach((item) => {
          const on = item.state === "enabled";
          const tag =
            item.rec === "insecure"
              ? '<span class="tag extreme">' + esc(t("feat.insecure")) + "</span>"
              : item.rec === "risky"
                ? '<span class="tag advanced">' + esc(t("feat.risky")) + "</span>"
                : item.rec === "off"
                  ? '<span class="tag safe">' + esc(t("feat.canOff")) + "</span>"
                  : "";

          const row = h(
            '<div class="item" data-name="' + esc(item.name) + '">' +
              '<div class="grow" style="min-width:0">' +
              '<div class="name">' + esc(item.title) + " " + tag + "</div>" +
              (item.desc ? '<div class="sub">' + esc(item.desc) + "</div>" : "") +
              '<div class="sub mono dim">' + esc(item.name) + "</div>" +
              "</div><div data-ctl></div></div>"
          );

          row.querySelector("[data-ctl]").appendChild(
            switchEl(on, async (next) => {
              if (!next && item.rec === "risky") {
                const ok = await confirmBox(item.title, item.desc || t("feat.confirmOff"), t("act.disable"), true);
                if (!ok) return false;
              }
              if (next && item.rec === "insecure") {
                const ok = await confirmBox(item.title, item.desc || "", t("act.enable"), true);
                if (!ok) return false;
              }

              const r = await invoke("features.set", { name: item.name, enable: next }, 900000);
              if (r.ok === false) {
                toast(item.title + ": " + (r.message || t("msg.failed")), "err");
                return false;
              }
              item.state = next ? "enabled" : "disabled";
              toast(item.title + ": " + (r.message || t("act.finish")));
              toast(t("msg.restartNeeded"), "warn");
              return true;
            })
          );

          list.appendChild(row);
        });

        body.appendChild(pane);
      });
    }

    el.addEventListener("click", (e) => {
      if (e.target.closest('[data-a="refresh"]')) load(true);
    });

    await load(false);
    return { el };
  }
};
