import { invoke, on } from "../bridge.js";
import { t } from "../i18n.js";
import { h, esc, num, toast, progress, confirmBox } from "../ui.js";
import { pulse, setEnergy } from "../scene.js";

const STAGES = {
  "cpu-single": "bench.cpu1",
  "cpu-multi": "bench.cpuN",
  ram: "bench.ram",
  disk: "bench.diskW",
  latency: "bench.lat",
  boot: "bench.boot",
  done: "act.finish"
};

export default {
  id: "bench",
  icon: "bench",
  group: "extra",

  async render() {
    const el = h('<div class="stack" style="gap:20px"></div>');
    el.appendChild(
      h(
        '<div class="page-head"><div><h1>' + esc(t("bench.title")) + '</h1>' +
          '<p class="lead">' + esc(t("bench.sub")) + "</p></div></div>"
      )
    );

    const runner = h(
      '<div class="pane stack">' +
        '<div class="row"><select data-label style="min-width:220px">' +
        '<option value="' + esc(t("bench.before")) + '">' + esc(t("bench.before")) + "</option>" +
        '<option value="' + esc(t("bench.after")) + '">' + esc(t("bench.after")) + "</option>" +
        "</select>" +
        '<button class="btn btn-primary" data-a="run">' + esc(t("bench.run")) + "</button>" +
        '<span class="small dim" data-stage></span></div>' +
        '<p class="small dim">' + esc(t("bench.warn")) + "</p>" +
        "</div>"
    );
    el.appendChild(runner);

    const latest = h('<div class="grid g4" data-latest></div>');
    el.appendChild(latest);

    const history = h('<div class="pane stack"><div class="row"><h2 class="grow">' + esc(t("bench.history")) + "</h2>" +
      '<button class="btn btn-ghost btn-sm" data-a="clear">' + esc(t("bench.clear")) + "</button></div>" +
      '<div class="list" data-hist></div></div>');
    el.appendChild(history);

    function card(label, value, unit, extra) {
      return (
        '<div class="metric"><div class="label">' + esc(label) + "</div>" +
        '<div class="value">' + esc(value) + (unit ? "<small>" + esc(unit) + "</small>" : "") + "</div>" +
        (extra ? '<div class="small dim">' + esc(extra) + "</div>" : "") +
        "</div>"
      );
    }

    function drawLatest(runs) {
      const box = latest;
      box.innerHTML = "";
      if (!runs.length) {
        box.appendChild(h('<div class="empty">' + esc(t("msg.empty")) + "</div>"));
        return;
      }
      const r = runs[0];
      const prev = runs.find((x) => x.label !== r.label);
      const delta = (a, b) => (prev && b ? (a > b ? "+" : "") + num(((a - b) / b) * 100, 1) + "%" : "");
      box.appendChild(h(card(t("bench.score"), num(r.score), "", r.label + " · " + new Date(r.utc).toLocaleString())));
      box.appendChild(h(card(t("bench.cpu1"), num(r.cpuSingle), "", prev ? delta(r.cpuSingle, prev.cpuSingle) : "")));
      box.appendChild(h(card(t("bench.cpuN"), num(r.cpuMulti), "", prev ? delta(r.cpuMulti, prev.cpuMulti) : "")));
      box.appendChild(h(card(t("bench.ram"), num(r.ramGbs, 2), t("unit.gbs"))));
      box.appendChild(h(card(t("bench.diskW"), num(r.diskWrite), t("unit.mbs"))));
      box.appendChild(h(card(t("bench.diskR"), num(r.diskRead), t("unit.mbs"))));
      box.appendChild(h(card(t("bench.lat"), num(r.latencyAvg, 2), t("unit.ms"), t("bench.latMax") + ": " + num(r.latencyMax, 2))));
      box.appendChild(h(card(t("bench.boot"), r.bootSeconds > 0 ? num(r.bootSeconds, 1) : "-", t("unit.sec"))));
    }

    function drawHistory(runs) {
      const box = history.querySelector("[data-hist]");
      box.innerHTML = "";
      if (!runs.length) {
        box.appendChild(h('<div class="empty small">' + esc(t("msg.empty")) + "</div>"));
        return;
      }
      runs.forEach((r) => {
        box.appendChild(
          h(
            '<div class="item"><div class="grow"><div class="name">' + esc(r.label) + "</div>" +
              '<div class="sub mono">' + new Date(r.utc).toLocaleString() + "</div></div>" +
              '<div class="mono small dim">CPU ' + num(r.cpuSingle) + " / " + num(r.cpuMulti) + "</div>" +
              '<div class="mono small dim">' + num(r.latencyAvg, 2) + " " + esc(t("unit.ms")) + "</div>" +
              '<div class="mono" style="min-width:70px;text-align:right">' + num(r.score) + "</div></div>"
          )
        );
      });
    }

    const off = on("progress", (p) => {
      const key = STAGES[p.stage];
      const el2 = runner.querySelector("[data-stage]");
      if (el2) el2.textContent = (key ? t(key) : p.stage) + " · " + p.percent + "%";
      progress(p.percent);
    });

    el.addEventListener("click", async (e) => {
      const btn = e.target.closest("[data-a]");
      if (!btn) return;
      if (btn.dataset.a === "clear") {
        const ok = await confirmBox(t("bench.clear"), t("bench.history"), t("act.confirm"), true);
        if (!ok) return;
        await invoke("bench.clear");
        drawHistory([]);
        drawLatest([]);
        return;
      }
      if (btn.dataset.a !== "run") return;
      btn.disabled = true;
      setEnergy(1);
      try {
        const label = runner.querySelector("[data-label]").value;
        await invoke("bench.run", { label }, 900000);
        const runs = await invoke("bench.history");
        drawLatest(runs);
        drawHistory(runs);
        toast(t("act.finish"));
        pulse(2500);
      } catch (err) {
        toast(String(err.message || err), "err");
      } finally {
        setEnergy(0);
        progress(100);
        runner.querySelector("[data-stage]").textContent = "";
        btn.disabled = false;
      }
    });

    const runs = await invoke("bench.history");
    drawLatest(runs);
    drawHistory(runs);
    return { el, dispose: off };
  }
};
