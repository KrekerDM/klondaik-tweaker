import { invoke } from "../bridge.js";
import { t } from "../i18n.js";
import { h, esc, toast, confirmBox } from "../ui.js";

export default {
  id: "power",
  icon: "power",
  group: "system",

  async render() {
    const el = h('<div class="stack" style="gap:20px;max-width:900px"></div>');

    el.appendChild(
      h(
        '<div class="page-head"><div><h1>' + esc(t("pwr.title")) + "</h1>" +
          '<p class="lead">' + esc(t("pwr.sub")) + "</p></div>" +
          '<div class="row"><button class="btn btn-ghost btn-sm" data-a="folder">' + esc(t("pwr.folder")) + "</button></div></div>"
      )
    );

    const body = h('<div class="stack"></div>');
    el.appendChild(body);

    async function load() {
      body.innerHTML = '<div class="stack"><div class="skel"></div><div class="skel"></div></div>';
      let data;
      try {
        data = await invoke("power.state", {}, 120000);
      } catch (err) {
        body.innerHTML = "";
        body.appendChild(h('<div class="pane"><p class="small dim mono">' + esc(String(err.message || err)) + "</p></div>"));
        return;
      }
      body.innerHTML = "";

      const schemes = h('<div class="pane stack"><h2>' + esc(t("pwr.schemes")) + '</h2><div class="list"></div></div>');
      const list = schemes.querySelector(".list");

      data.schemes.forEach((s) => {
        list.appendChild(
          h(
            '<div class="item' + (s.active ? " on" : "") + '">' +
              '<div class="grow" style="min-width:0">' +
              '<div class="name">' + esc(s.name) +
              (s.active ? ' <span class="tag applied">' + esc(t("pwr.active")) + "</span>" : "") + "</div>" +
              '<div class="sub mono dim">' + esc(s.guid) + "</div></div>" +
              '<div class="row">' +
              (s.active ? "" : '<button class="btn btn-ghost btn-sm" data-use="' + esc(s.guid) + '">' + esc(t("pwr.use")) + "</button>") +
              '<button class="btn btn-ghost btn-sm" data-exp="' + esc(s.guid) + '">' + esc(t("pwr.export")) + "</button>" +
              (s.active ? "" : '<button class="btn btn-ghost btn-sm" data-del="' + esc(s.guid) + '">' + esc(t("act.remove")) + "</button>") +
              "</div></div>"
          )
        );
      });
      body.appendChild(schemes);

      const files = h(
        '<div class="pane stack"><h2>' + esc(t("pwr.files")) + "</h2>" +
          '<p class="small dim">' + esc(t("pwr.filesHint")) + "</p>" +
          (data.files.length
            ? '<div class="list">' + data.files.map((f) =>
                '<div class="item"><div class="grow mono" style="min-width:0">' + esc(f) + "</div>" +
                '<button class="btn btn-ghost btn-sm" data-imp="' + esc(f) + '">' + esc(t("pwr.import")) + "</button></div>"
              ).join("") + "</div>"
            : '<div class="empty">' + esc(t("pwr.noFiles")) + "</div>") +
          "</div>"
      );
      body.appendChild(files);

      const hidden = h(
        '<div class="pane stack"><h2>' + esc(t("pwr.hiddenTitle")) + "</h2>" +
          '<p class="small dim">' + esc(t("pwr.hiddenHint")) + "</p>" +
          '<div class="row"><span class="grow small">' +
          (data.hidden > 0
            ? esc(t("pwr.hiddenCount")) + ": " + data.hidden
            : esc(t("pwr.allShown"))) +
          "</span>" +
          (data.hidden > 0
            ? '<button class="btn btn-primary btn-sm" data-a="unhide">' + esc(t("pwr.unhide")) + "</button>"
            : "") +
          "</div></div>"
      );
      body.appendChild(hidden);
    }

    el.addEventListener("click", async (e) => {
      const btn = e.target.closest("button");
      if (!btn) return;

      if (btn.dataset.a === "folder") return void invoke("power.folder");

      if (btn.dataset.a === "unhide") {
        const ok = await confirmBox(t("pwr.hiddenTitle"), t("pwr.unhideConfirm"), t("pwr.unhide"), false);
        if (!ok) return;
        const r = await invoke("power.unhide", {}, 180000);
        toast(r.message || "", r.ok === false ? "err" : "");
        return void (await load());
      }

      if (btn.dataset.use) {
        const r = await invoke("power.activate", { guid: btn.dataset.use }, 60000);
        toast(r.message || "", r.ok === false ? "err" : "");
        return void (await load());
      }

      if (btn.dataset.exp) {
        const r = await invoke("power.export", { guid: btn.dataset.exp }, 60000);
        toast(r.message || "", r.ok === false ? "err" : "");
        return void (await load());
      }

      if (btn.dataset.imp) {
        const r = await invoke("power.import", { file: btn.dataset.imp }, 60000);
        toast(r.message || "", r.ok === false ? "err" : "");
        return void (await load());
      }

      if (btn.dataset.del) {
        const ok = await confirmBox(t("pwr.deleteTitle"), t("pwr.deleteConfirm"), t("act.remove"), true);
        if (!ok) return;
        const r = await invoke("power.delete", { guid: btn.dataset.del }, 60000);
        toast(r.message || "", r.ok === false ? "err" : "");
        await load();
      }
    });

    await load();
    return { el };
  }
};
