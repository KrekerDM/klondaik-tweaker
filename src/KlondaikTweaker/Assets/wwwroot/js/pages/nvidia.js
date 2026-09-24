import { invoke } from "../bridge.js";
import { t } from "../i18n.js";
import { h, esc, toast, confirmBox } from "../ui.js";

export default {
  id: "nvidia",
  icon: "nvidia",
  group: "system",

  async render() {
    const el = h('<div class="stack" style="gap:20px;max-width:900px"></div>');

    el.appendChild(
      h(
        '<div class="page-head"><div><h1>' + esc(t("nv.title")) + "</h1>" +
          '<p class="lead">' + esc(t("nv.sub")) + "</p></div>" +
          '<div class="row"><button class="btn btn-ghost btn-sm" data-a="folder">' + esc(t("nv.folder")) + "</button></div></div>"
      )
    );

    const body = h('<div class="stack"></div>');
    el.appendChild(body);

    async function load() {
      body.innerHTML = '<div class="stack"><div class="skel"></div></div>';
      let s;
      try {
        s = await invoke("nvidia.state", {}, 60000);
      } catch (err) {
        body.innerHTML = "";
        body.appendChild(h('<div class="pane"><p class="small dim mono">' + esc(String(err.message || err)) + "</p></div>"));
        return;
      }

      body.innerHTML = "";

      if (!s.present) {
        body.appendChild(h('<div class="empty">' + esc(t("nv.noGpu")) + "</div>"));
        return;
      }

      body.appendChild(
        h(
          '<div class="pane stack"><h2>' + esc(t("nv.tool")) + "</h2>" +
            '<p class="small dim">' + esc(t("nv.toolHint")) + "</p>" +
            '<div class="spec">' +
            "<div><dt>" + esc(t("nv.gpu")) + "</dt><dd>" + esc(s.gpu || "NVIDIA") + "</dd></div>" +
            "<div><dt>Profile Inspector</dt><dd>" + esc(s.version || "—") + "</dd></div>" +
            "</div>" +
            '<div class="row" style="margin-top:8px">' +
            '<button class="btn btn-primary" data-a="export">' + esc(t("nv.export")) + "</button>" +
            '<button class="btn btn-ghost" data-a="open">' + esc(t("nv.open")) + "</button>" +
            "</div></div>"
        )
      );

      body.appendChild(
        h(
          '<div class="pane stack"><h2>' + esc(t("nv.profiles")) + "</h2>" +
            '<p class="small dim">' + esc(t("nv.profilesHint")) + "</p>" +
            (s.files.length
              ? '<div class="list">' + s.files.map((f) =>
                  '<div class="item"><div class="grow mono" style="min-width:0">' + esc(f) + "</div>" +
                  '<button class="btn btn-ghost btn-sm" data-imp="' + esc(f) + '">' + esc(t("nv.import")) + "</button></div>"
                ).join("") + "</div>"
              : '<div class="empty">' + esc(t("nv.noFiles")) + "</div>") +
            "</div>"
        )
      );

      body.appendChild(
        h('<div class="warn plain small">' + esc(t("nv.credit")) + "</div>")
      );
    }

    el.addEventListener("click", async (e) => {
      const btn = e.target.closest("button");
      if (!btn) return;

      if (btn.dataset.a === "folder") return void invoke("nvidia.folder");

      if (btn.dataset.a === "open") {
        const r = await invoke("nvidia.open", {}, 60000);
        return void toast(r.message || "", r.ok === false ? "err" : "");
      }

      if (btn.dataset.a === "export") {
        const r = await invoke("nvidia.export", {}, 240000);
        toast(r.message || "", r.ok === false ? "err" : "");
        return void (await load());
      }

      if (btn.dataset.imp) {
        const ok = await confirmBox(btn.dataset.imp, t("nv.importConfirm"), t("nv.import"), false);
        if (!ok) return;
        const r = await invoke("nvidia.import", { file: btn.dataset.imp }, 240000);
        toast(r.message || "", r.ok === false ? "err" : "");
        await load();
      }
    });

    await load();
    return { el };
  }
};
