import { invoke } from "../bridge.js";
import { t, getLang } from "../i18n.js";
import { h, esc, bytes, toast, confirmBox, progress } from "../ui.js";
import { pulse } from "../scene.js";

export default {
  id: "clean",
  icon: "clean",
  group: "main",

  async render() {
    const el = h('<div class="stack" style="gap:20px"></div>');
    el.appendChild(
      h(
        '<div class="page-head"><div><h1>' + esc(t("clean.title")) + '</h1>' +
          '<p class="lead">' + esc(t("clean.sub")) + "</p></div>" +
          '<button class="btn btn-ghost" data-a="scan">' + esc(t("clean.scan")) + "</button></div>"
      )
    );

    const drives = h('<div class="grid g4"></div>');
    el.appendChild(drives);

    const list = h('<div class="list"></div>');
    el.appendChild(list);

    const extra = h(
      '<div class="grid g2">' +
        '<div class="card stack-sm"><h3>' + esc(t("clean.deep")) + "</h3>" +
        '<p class="small dim">' + esc(t("clean.deepHint")) + "</p>" +
        '<div><button class="btn btn-ghost btn-sm" data-a="deep">' + esc(t("act.run")) + "</button></div></div>" +
        '<div class="card stack-sm"><h3>' + esc(t("clean.logs")) + "</h3>" +
        '<p class="small dim">Application, System, Security</p>' +
        '<div><button class="btn btn-ghost btn-sm" data-a="logs">' + esc(t("act.run")) + "</button></div></div>" +
        "</div>"
    );
    el.appendChild(extra);

    const bar = h(
      '<div class="sticky-actions">' +
        '<span class="small dim" data-total></span>' +
        '<div class="grow"></div>' +
        '<button class="btn btn-primary" data-a="clean">' + esc(t("clean.run")) + "</button>" +
        "</div>"
    );
    el.appendChild(bar);

    function updateTotal() {
      let sum = 0;
      list.querySelectorAll(".item").forEach((row) => {
        if (row.querySelector(".cb.on")) sum += Number(row.dataset.bytes || 0);
      });
      bar.querySelector("[data-total]").textContent = t("clean.found") + ": " + bytes(sum);
    }

    async function scan() {
      list.innerHTML = '<div class="skel"></div><div class="skel"></div><div class="skel"></div>';
      drives.innerHTML = "";
      const data = await invoke("clean.scan");

      (data.disks.drives || []).forEach((d) => {
        const used = d.total > 0 ? Math.round(((d.total - d.free) / d.total) * 100) : 0;
        drives.appendChild(
          h(
            '<div class="metric"><div class="label">' + esc(d.name + " " + (d.label || "")) + "</div>" +
              '<div class="value">' + used + "<small>%</small></div>" +
              '<div class="bar' + (used > 90 ? " crit" : used > 75 ? " hot" : "") + '"><i style="width:' + used + '%"></i></div>' +
              '<div class="small dim">' + bytes(d.free) + " free / " + bytes(d.total) + "</div></div>"
          )
        );
      });

      list.innerHTML = "";
      data.targets.forEach((target) => {
        const title = getLang() === "en" ? target.en : target.ru;
        const desc = getLang() === "en" ? target.descEn : target.descRu;
        const row = h(
          '<div class="item" data-id="' + esc(target.id) + '" data-bytes="' + target.bytes + '">' +
            '<div class="cb' + (target.default && target.bytes > 0 ? " on" : "") + '"></div>' +
            '<div class="grow"><div class="name">' + esc(title) +
            (target.safe ? "" : ' <span class="tag advanced">' + esc(t("risk.advanced")) + "</span>") +
            '</div><div class="sub">' + esc(desc) + "</div></div>" +
            '<div class="mono" style="text-align:right"><div>' + bytes(target.bytes) + "</div>" +
            '<div class="small dim">' + target.files + " " + esc(t("clean.files")) + "</div></div>" +
            "</div>"
        );
        row.querySelector(".cb").addEventListener("click", (e) => {
          e.currentTarget.classList.toggle("on");
          updateTotal();
        });
        list.appendChild(row);
      });
      updateTotal();
    }

    el.addEventListener("click", async (e) => {
      const btn = e.target.closest("[data-a]");
      if (!btn) return;
      const action = btn.dataset.a;
      btn.disabled = true;
      try {
        if (action === "scan") {
          await scan();
        } else if (action === "clean") {
          const ids = [...list.querySelectorAll(".item")].filter((x) => x.querySelector(".cb.on")).map((x) => x.dataset.id);
          if (!ids.length) return toast(t("msg.nothingSelected"), "warn");
          if (ids.includes("winold")) {
            const ok = await confirmBox("Windows.old", t("clean.sub"), t("act.remove"), true);
            if (!ok) return;
          }
          progress(10);
          pulse(3000);
          const r = await invoke("clean.run", { ids });
          progress(100);
          toast(t("clean.freed") + ": " + bytes(r.freed) + " · " + r.files + " " + t("clean.files"));
          if (r.errors && r.errors.length) console.warn("clean errors", r.errors);
          await scan();
        } else if (action === "deep") {
          const ok = await confirmBox(t("clean.deep"), t("clean.deepHint"), t("act.run"), true);
          if (!ok) return;
          progress(10);
          toast(t("bench.running"));
          const r = await invoke("clean.deep", {}, 2400000);
          progress(100);
          toast(r.result === "ok" ? t("act.finish") : r.result, r.result === "ok" ? "" : "err");
          await scan();
        } else if (action === "logs") {
          const ok = await confirmBox(t("clean.logs"), t("clean.logs"), t("act.run"), true);
          if (!ok) return;
          const r = await invoke("clean.eventlogs");
          toast(r.result === "ok" ? t("act.finish") : r.result);
        }
      } catch (err) {
        toast(String(err.message || err), "err");
      } finally {
        btn.disabled = false;
      }
    });

    await scan();
    return { el };
  }
};
