import { invoke, on } from "../bridge.js";
import { t } from "../i18n.js";
import { h, esc, bytes, num, duration, toast, barClass } from "../ui.js";
import { pulse } from "../scene.js";

function metric(id, label, value, unit) {
  return (
    '<div class="metric" data-m="' + id + '">' +
    '<div class="label">' + esc(label) + "</div>" +
    '<div class="value"><span data-v>' + value + "</span>" + (unit ? "<small>" + esc(unit) + "</small>" : "") + "</div>" +
    '<div class="bar"><i style="width:0"></i></div>' +
    '<div class="small dim" data-sub></div>' +
    "</div>"
  );
}

export default {
  id: "dash",
  icon: "dash",
  group: "main",

  async render(app) {
    const f = app.info.facts;
    const el = h('<div class="stack" style="gap:24px"></div>');

    el.appendChild(
      h(
        '<div class="page-head"><div><h1>' +
          esc(t("dash.title")) +
          '</h1><p class="lead">' +
          esc(t("dash.sub")) +
          "</p></div></div>"
      )
    );

    const metrics = h(
      '<div class="grid g4">' +
        metric("cpu", t("dash.cpu"), "0", "%") +
        metric("ram", t("dash.ram"), "0", "%") +
        metric("gpu", t("dash.gpu"), "0", "%") +
        metric("disk", t("dash.disk"), "0", "%") +
        "</div>"
    );
    el.appendChild(metrics);

    const boostOn = !!app.info.boost;
    const actions = h(
      '<div class="grid g2" style="align-items:start">' +
        '<div class="pane stack">' +
        "<h2>" + esc(t("dash.quick")) + "</h2>" +
        '<div class="row" style="gap:10px">' +
        '<button class="btn ' + (boostOn ? "btn-danger" : "btn-primary") + '" data-a="boost">' +
        esc(boostOn ? t("dash.boostOff") : t("dash.boost")) +
        "</button>" +
        '<button class="btn btn-ghost" data-a="trim">' + esc(t("dash.trim")) + "</button>" +
        '<button class="btn btn-ghost" data-a="clean">' + esc(t("dash.cleanNow")) + "</button>" +
        '<button class="btn btn-ghost" data-a="wizard">' + esc(t("dash.wizard")) + "</button>" +
        "</div>" +
        '<div class="small dim" data-boost-note></div>' +
        "</div>" +
        '<div class="pane stack">' +
        "<h2>" + esc(t("dash.protection")) + "</h2>" +
        '<div class="list">' +
        '<div class="item"><div class="grow"><div class="name">' + esc(t("dash.applied")) + '</div></div><div class="mono">' + app.info.journal + "</div></div>" +
        '<div class="item"><div class="grow"><div class="name">' + esc(t("dash.restorePoint")) + '</div><div class="sub" data-rp></div></div>' +
        '<button class="btn btn-ghost btn-sm" data-a="point">' + esc(t("dash.createPoint")) + "</button></div>" +
        '<div class="item"><div class="grow"><div class="name">Windows Defender</div><div class="sub">' +
        esc(f.defenderReal ? (f.tamperProtection ? "Real-time + Tamper Protection" : "Real-time") : "off") +
        '</div></div><span class="tag ' + (f.defenderReal ? "safe" : "extreme") + '">' + (f.defenderReal ? "on" : "off") + "</span></div>" +
        '<div class="item"><div class="grow"><div class="name">VBS / Core isolation</div><div class="sub">Hypervisor security</div></div>' +
        '<span class="tag ' + (f.vbsRunning ? "advanced" : "plain") + '">' + (f.vbsRunning ? "on" : "off") + "</span></div>" +
        "</div></div>" +
        "</div>"
    );
    el.appendChild(actions);

    const sys = h(
      '<div class="pane stack">' +
        "<h2>" + esc(t("dash.system")) + "</h2>" +
        '<div class="spec">' +
        spec("CPU", f.cpu + " · " + f.cores + "C/" + f.threads + "T") +
        spec("GPU", (f.gpus && f.gpus[0]) || "-") +
        spec("RAM", f.ramGb + " " + t("unit.gb")) +
        spec("Motherboard", f.motherboard || "-") +
        spec("Windows", f.osVersion) +
        spec("Edition", f.edition) +
        spec(t("dash.uptime"), '<span data-uptime>-</span>', true) +
        spec(t("dash.processes"), '<span data-proc>-</span>', true) +
        spec("Power plan", f.powerPlan || "-") +
        spec("System drive", f.systemSsd ? "SSD" : "HDD") +
        spec("Secure Boot", f.secureBoot ? "on" : "off") +
        spec("Type", f.laptop ? "Laptop" : "Desktop") +
        "</div></div>"
    );
    el.appendChild(sys);

    const rpNote = el.querySelector("[data-rp]");
    invoke("restore.status")
      .then((r) => {
        rpNote.textContent = r.enabled
          ? (r.points[0] ? r.points[0].time + " · " + r.points[0].desc : "-")
          : "System Restore off";
      })
      .catch(() => {});

    const setMetric = (id, value, sub) => {
      const box = metrics.querySelector('[data-m="' + id + '"]');
      if (!box) return;
      box.querySelector("[data-v]").textContent = value;
      const bar = box.querySelector(".bar");
      bar.className = barClass(Number(value) || 0);
      bar.firstElementChild.style.width = Math.min(100, Number(value) || 0) + "%";
      if (sub != null) box.querySelector("[data-sub]").textContent = sub;
    };

    const off = on("metrics", (m) => {
      setMetric("cpu", m.cpu, m.cpuTemp > 0 ? m.cpuTemp + " °C" : f.cpu.split(" ").slice(0, 3).join(" "));
      setMetric("ram", m.ram, bytes(m.ramUsed) + " / " + bytes(m.ramTotal));
      setMetric("gpu", m.gpu, m.gpuTemp > 0 ? m.gpuTemp + " °C" : (m.gpuMem ? m.gpuMem + " " + t("unit.mb") : ""));
      setMetric("disk", m.disk, "↓ " + num(m.netDown) + " · ↑ " + num(m.netUp) + " " + t("unit.kbs"));
      const up = el.querySelector("[data-uptime]");
      if (up) up.textContent = duration(m.uptime);
      const pr = el.querySelector("[data-proc]");
      if (pr) pr.textContent = m.processes;
    });

    el.addEventListener("click", async (e) => {
      const btn = e.target.closest("[data-a]");
      if (!btn) return;
      const action = btn.dataset.a;
      if (action === "wizard") return app.go("wizard");
      if (action === "clean") return app.go("clean");

      btn.disabled = true;
      try {
        if (action === "boost") {
          const active = app.info.boost;
          const r = active ? await invoke("boost.stop") : await invoke("boost.start");
          app.info.boost = !!r.active;
          btn.textContent = r.active ? t("dash.boostOff") : t("dash.boost");
          btn.className = "btn " + (r.active ? "btn-danger" : "btn-primary");
          const note = el.querySelector("[data-boost-note]");
          note.textContent = r.active
            ? Object.keys(r.services || {}).length + " services paused · " + bytes(r.freedRam) + " freed"
            : "";
          if (r.active) pulse(3000);
          toast(r.active ? t("dash.boostOn") : t("msg.reverted"));
        } else if (action === "trim") {
          const r = await invoke("monitor.trim");
          toast(t("clean.freed") + ": " + bytes(r.freed) + " · " + r.processes);
          pulse(1800);
        } else if (action === "point") {
          const r = await invoke("restore.create", { desc: "Klondaik Tweaker manual" });
          toast(r.ok ? t("msg.pointCreated") : t("msg.pointFailed") + ": " + r.message, r.ok ? "" : "err");
          if (r.ok) invoke("restore.status").then((s) => {
            rpNote.textContent = s.points[0] ? s.points[0].time + " · " + s.points[0].desc : "-";
          });
        }
      } catch (err) {
        toast(String(err.message || err), "err");
      } finally {
        btn.disabled = false;
      }
    });

    return { el, dispose: off };
  }
};

function spec(label, value, raw) {
  return "<div><dt>" + esc(label) + "</dt><dd>" + (raw ? value : esc(value)) + "</dd></div>";
}
