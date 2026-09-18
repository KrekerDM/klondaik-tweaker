import { invoke } from "../bridge.js";
import { t } from "../i18n.js";
import { h, esc, toast, confirmBox, progress } from "../ui.js";

export default {
  id: "journal",
  icon: "journal",
  group: "extra",

  async render(app) {
    const el = h('<div class="stack" style="gap:20px"></div>');
    el.appendChild(
      h(
        '<div class="page-head"><div><h1>' + esc(t("jr.title")) + '</h1>' +
          '<p class="lead">' + esc(t("jr.sub")) + "</p></div>" +
          '<div class="row"><button class="btn btn-ghost" data-a="clear">' + esc(t("jr.clear")) + "</button>" +
          '<button class="btn btn-danger" data-a="revertAll">' + esc(t("jr.revertAll")) + "</button></div></div>"
      )
    );

    const restore = h('<div class="pane stack"><div class="row"><h2 class="grow">' + esc(t("jr.restore")) + "</h2>" +
      '<button class="btn btn-ghost btn-sm" data-a="point">' + esc(t("dash.createPoint")) + "</button>" +
      '<button class="btn btn-ghost btn-sm" data-a="rstrui">' + esc(t("set.systemRestore")) + "</button></div>" +
      '<div class="list" data-points></div></div>');
    el.appendChild(restore);

    const list = h('<div class="list" data-list></div>');
    el.appendChild(list);

    async function loadPoints() {
      const box = restore.querySelector("[data-points]");
      box.innerHTML = '<div class="skel" style="height:36px"></div>';
      try {
        const r = await invoke("restore.status");
        box.innerHTML = "";
        if (!r.enabled) {
          box.appendChild(h('<div class="item"><div class="grow"><div class="name">System Restore off</div></div>' +
            '<button class="btn btn-ghost btn-sm" data-a="enableRp">' + esc(t("act.enable")) + "</button></div>"));
        }
        if (!r.points.length) {
          box.appendChild(h('<div class="empty small">' + esc(t("msg.empty")) + "</div>"));
          return;
        }
        r.points.forEach((p) => {
          box.appendChild(
            h('<div class="item"><div class="grow"><div class="name">' + esc(p.desc) + "</div>" +
              '<div class="sub mono">' + esc(p.time) + "</div></div>" +
              '<span class="tag plain mono">#' + p.seq + "</span></div>")
          );
        });
      } catch {
        box.innerHTML = "";
      }
    }

    async function load() {
      list.innerHTML = '<div class="skel"></div><div class="skel"></div>';
      const entries = await invoke("journal.list");
      list.innerHTML = "";
      if (!entries.length) {
        list.appendChild(h('<div class="empty">' + esc(t("jr.empty")) + "</div>"));
        return;
      }
      entries.forEach((e) => {
        list.appendChild(
          h(
            '<div class="item" data-id="' + esc(e.id) + '">' +
              '<div class="grow" style="min-width:0"><div class="name">' + esc(e.title || e.tweakId) +
              ' <span class="tag ' + esc(e.risk) + '">' + esc(t("risk." + e.risk)) + "</span>" +
              (e.reverted ? ' <span class="tag plain">' + esc(t("jr.reverted")) + "</span>" : "") +
              '</div><div class="sub mono">' + esc(e.tweakId) + " · " + esc(e.time) + " · " + e.items + "</div></div>" +
              (e.reverted ? "" : '<button class="btn btn-ghost btn-sm" data-rev>' + esc(t("act.revert")) + "</button>") +
              "</div>"
          )
        );
      });
    }

    el.addEventListener("click", async (e) => {
      const rev = e.target.closest("[data-rev]");
      if (rev) {
        const row = rev.closest("[data-id]");
        rev.disabled = true;
        const r = await invoke("journal.revert", { id: row.dataset.id });
        if (r.ok) {
          toast(t("msg.reverted"));
          await app.refresh();
          await load();
        } else {
          toast(t("msg.failed") + ": " + (r.error || ""), "err");
          rev.disabled = false;
        }
        return;
      }

      const btn = e.target.closest("[data-a]");
      if (!btn) return;
      const action = btn.dataset.a;
      btn.disabled = true;
      try {
        if (action === "revertAll") {
          const ok = await confirmBox(t("jr.revertAll"), t("jr.sub"), t("act.revert"), true);
          if (!ok) return;
          progress(5);
          const r = await invoke("journal.revertAll", {}, 900000);
          progress(100);
          toast(t("msg.reverted") + ": " + r.reverted + (r.failed ? " · " + t("msg.failed") + ": " + r.failed : ""));
          await app.refresh();
          await load();
        } else if (action === "clear") {
          const ok = await confirmBox(t("jr.clear"), t("jr.clearHint"), t("act.confirm"), true);
          if (!ok) return;
          await invoke("journal.clear");
          await app.refresh();
          await load();
        } else if (action === "point") {
          const r = await invoke("restore.create", { desc: "Klondaik Tweaker manual" });
          toast(r.ok ? t("msg.pointCreated") : t("msg.pointFailed") + ": " + r.message, r.ok ? "" : "err");
          await loadPoints();
        } else if (action === "enableRp") {
          const r = await invoke("restore.enable", {}, 300000);
          toast(r.result === "ok" ? t("act.enable") : r.result, r.result === "ok" ? "" : "err");
          await loadPoints();
        } else if (action === "rstrui") {
          await invoke("restore.open");
        }
      } catch (err) {
        toast(String(err.message || err), "err");
      } finally {
        btn.disabled = false;
      }
    });

    await Promise.all([load(), loadPoints()]);
    return { el };
  }
};
