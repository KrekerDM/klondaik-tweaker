import { invoke, on } from "../bridge.js";
import { t } from "../i18n.js";
import { h, esc, bytes, num, duration, toast, barClass } from "../ui.js";

const LIMIT = 90;

function metric(id, label) {
  return (
    '<div class="metric" data-m="' + id + '">' +
    '<div class="label">' + esc(label) + "</div>" +
    '<div class="value"><span data-v>0</span><span class="num-unit">%</span></div>' +
    '<div class="bar"><i style="width:0"></i></div>' +
    '<div class="num-threshold">' + esc(t("dash.limit")) + " " + LIMIT + "</div>" +
    '<div class="small dim" data-sub></div>' +
    "</div>"
  );
}

function band(word, body) {
  return '<section class="pane"><h2>' + esc(word) + "</h2>" + body + "</section>";
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

    const boostOn = !!app.info.boost;

    el.appendChild(
      h(
        band(
          t("dash.load"),
          '<div class="grid g4">' +
            metric("cpu", t("dash.cpu")) +
            metric("ram", t("dash.ram")) +
            metric("gpu", t("dash.gpu")) +
            metric("disk", t("dash.disk")) +
            "</div>"
        )
      )
    );

    el.appendChild(
      h(
        band(
          t("dash.quick"),
          '<div class="row">' +
            '<button class="btn ' + (boostOn ? "btn-danger" : "btn-primary") + '" data-a="boost">' +
            esc(boostOn ? t("dash.boostOff") : t("dash.boost")) +
            "</button>" +
            '<button class="btn" data-a="trim">' + esc(t("dash.trim")) + "</button>" +
            '<button class="btn" data-a="clean">' + esc(t("dash.cleanNow")) + "</button>" +
            '<button class="btn" data-a="wizard">' + esc(t("dash.wizard")) + "</button>" +
            "</div>" +
            '<div class="small dim" data-boost-note></div>'
        )
      )
    );

    el.appendChild(
      h(
        band(
          t("dash.protection"),
          '<div class="list">' +
            '<div class="item"><div class="grow"><div class="name lc">' +
            esc(t("dash.applied")) +
            '</div></div><div class="mono">' + app.info.journal + "</div></div>" +
            '<div class="item"><div class="grow"><div class="name lc">' +
            esc(t("dash.restorePoint")) +
            '</div><div class="sub" data-rp></div></div>' +
            '<button class="btn btn-sm" data-a="point">' + esc(t("dash.createPoint")) + "</button></div>" +
            '<div class="item"><div class="grow"><div class="name">Windows Defender</div><div class="sub">' +
            esc(f.defenderReal ? (f.tamperProtection ? "Real-time + Tamper Protection" : "Real-time") : "off") +
            '</div></div><span class="tag ' + (f.defenderReal ? "safe" : "extreme") + '">' +
            (f.defenderReal ? "on" : "off") + "</span></div>" +
            '<div class="item"><div class="grow"><div class="name">VBS / Core isolation</div><div class="sub">Hypervisor security</div></div>' +
            '<span class="tag ' + (f.vbsRunning ? "advanced" : "plain") + '">' +
            (f.vbsRunning ? "on" : "off") + "</span></div>" +
            "</div>"
        )
      )
    );

    el.appendChild(
      h(
        band(
          t("dash.system"),
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
            "</div>"
        )
      )
    );

    const rpNote = el.querySelector("[data-rp]");
    invoke("restore.status")
      .then((r) => {
        rpNote.textContent = r.enabled
          ? (r.points[0] ? r.points[0].time + " · " + r.points[0].desc : "-")
          : "System Restore off";
      })
      .catch(() => {});

    const setMetric = (id, value, sub) => {
      const box = el.querySelector('[data-m="' + id + '"]');
      if (!box) return;
      const n = Number(value) || 0;
      const shown = box.querySelector("[data-v]");
      shown.textContent = value;
      shown.parentElement.classList.toggle("num-bad", n >= LIMIT);
      const bar = box.querySelector(".bar");
      bar.className = barClass(n);
      bar.firstElementChild.style.width = Math.min(100, n) + "%";
      if (sub != null) box.querySelector("[data-sub]").textContent = sub;
    };

    const off = on("metrics", (m) => {
      setMetric("cpu", m.cpu, m.cpuTemp > 0 ? m.cpuTemp + " °C" : f.cpu.split(" ").slice(0, 3).join(" "));
      setMetric("ram", m.ram, bytes(m.ramUsed) + " / " + bytes(m.ramTotal));
      setMetric("gpu", m.gpu, m.gpuTemp > 0 ? m.gpuTemp + " °C" : (m.gpuMem ? m.gpuMem + " " + t("unit.mb") : ""));
      setMetric(
        "disk",
        m.disk,
        t("dash.netIn") + " " + num(m.netDown) + "  ·  " + t("dash.netOut") + " " + num(m.netUp) + " " + t("unit.kbs")
      );
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
            ? Object.keys(r.services || {}).length + " " + t("dash.boostPaused") + "  ·  " +
              bytes(r.freedRam) + " " + t("dash.boostFreed")
            : "";
          toast(r.active ? t("dash.boostOn") : t("msg.reverted"));
        } else if (action === "trim") {
          const r = await invoke("monitor.trim");
          toast(t("clean.freed") + ": " + bytes(r.freed) + " · " + r.processes);
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
