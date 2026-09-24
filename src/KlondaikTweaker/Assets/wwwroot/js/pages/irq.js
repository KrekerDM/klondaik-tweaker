import { invoke } from "../bridge.js";
import { t } from "../i18n.js";
import { h, esc, toast, confirmBox } from "../ui.js";

export default {
  id: "irq",
  icon: "irq",
  group: "system",

  async render() {
    const el = h('<div class="stack" style="gap:20px;max-width:980px"></div>');

    el.appendChild(
      h(
        '<div class="page-head"><div><h1>' + esc(t("irq.title")) + "</h1>" +
          '<p class="lead">' + esc(t("irq.sub")) + "</p></div></div>"
      )
    );
    el.appendChild(h('<div class="warn">' + esc(t("irq.warn")) + "</div>"));

    const body = h('<div class="stack"></div>');
    el.appendChild(body);

    let data = null;
    let selected = null;
    let picked = new Set();

    async function load() {
      body.innerHTML = '<div class="stack"><div class="skel"></div><div class="skel"></div></div>';
      try {
        data = await invoke("irq.list", {}, 120000);
      } catch (err) {
        body.innerHTML = "";
        body.appendChild(h('<div class="pane"><p class="small dim mono">' + esc(String(err.message || err)) + "</p></div>"));
        return;
      }
      if (!selected && data.devices.length) selected = data.devices[0].id;
      draw();
    }

    function draw() {
      const device = data.devices.find((x) => x.id === selected) || null;
      picked = new Set(device ? device.threads : []);
      body.innerHTML = "";

      const devPane = h('<div class="pane stack"><h2>' + esc(t("irq.devices")) + "</h2><div class=\"list\"></div></div>");
      const list = devPane.querySelector(".list");

      data.devices.forEach((d) => {
        const on = d.id === selected;
        const state = d.bound
          ? t("irq.boundTo") + ": " + d.threads.join(", ")
          : t("irq.notBound");
        const row = h(
          '<div class="item' + (on ? " on" : "") + '" data-dev="' + esc(d.id) + '" style="cursor:pointer">' +
            '<div class="grow" style="min-width:0">' +
            '<div class="name">' + esc(d.name) + ' <span class="tag plain">' + esc(t("irq.kind." + d.kind)) + "</span></div>" +
            '<div class="sub">' + esc(state) + (d.priority === 3 ? " · " + esc(t("irq.highPriority")) : "") + "</div>" +
            "</div></div>"
        );
        list.appendChild(row);
      });

      list.addEventListener("click", (e) => {
        const row = e.target.closest("[data-dev]");
        if (!row) return;
        selected = row.dataset.dev;
        draw();
      });
      body.appendChild(devPane);

      if (!device) {
        body.appendChild(h('<div class="empty">' + esc(t("irq.none")) + "</div>"));
        return;
      }

      const cores = new Map();
      data.threads.forEach((th) => {
        if (!cores.has(th.core)) cores.set(th.core, []);
        cores.get(th.core).push(th);
      });

      const grid = h('<div class="irqgrid"></div>');
      [...cores.entries()].forEach(([core, list2]) => {
        const perf = list2[0].performance;
        list2.forEach((th) => {
          const cell = h(
            '<button class="irqcell' + (perf ? " perf" : " eff") + (picked.has(th.index) ? " on" : "") +
              '" data-thread="' + th.index + '">' +
              '<b>' + th.index + "</b>" +
              '<i>' + esc(t("irq.core")) + " " + core + "</i>" +
              '<u>' + (perf ? "P" : "E") + "</u></button>"
          );
          grid.appendChild(cell);
        });
      });

      const pane = h(
        '<div class="pane stack">' +
          "<h2>" + esc(device.name) + "</h2>" +
          '<p class="small dim">' + esc(t("irq.pick")) + "</p>" +
          '<div data-grid></div>' +
          '<label class="row" style="gap:8px;align-items:center"><input type="checkbox" data-prio' +
          (device.priority === 3 ? " checked" : "") + '> <span class="small">' + esc(t("irq.priorityHint")) + "</span></label>" +
          '<div class="row" style="margin-top:8px">' +
          '<button class="btn btn-primary" data-a="bind">' + esc(t("irq.bind")) + "</button>" +
          '<button class="btn btn-ghost" data-a="reset">' + esc(t("irq.reset")) + "</button>" +
          '<div class="grow"></div>' +
          '<span class="small dim mono" data-sum></span>' +
          "</div>" +
          '<p class="small dim mono">' + esc(device.id) + "</p></div>"
      );
      pane.querySelector("[data-grid]").appendChild(grid);

      const sum = pane.querySelector("[data-sum]");
      const refreshSum = () => {
        const sorted = [...picked].sort((a, b) => a - b);
        sum.textContent = sorted.length ? t("irq.chosen") + ": " + sorted.join(", ") : t("irq.chooseAtLeastOne");
      };
      refreshSum();

      grid.addEventListener("click", (e) => {
        const cell = e.target.closest("[data-thread]");
        if (!cell) return;
        const index = Number(cell.dataset.thread);
        if (picked.has(index)) picked.delete(index);
        else picked.add(index);
        cell.classList.toggle("on", picked.has(index));
        refreshSum();
      });

      pane.addEventListener("click", async (e) => {
        const btn = e.target.closest("[data-a]");
        if (!btn) return;

        if (btn.dataset.a === "reset") {
          const r = await invoke("irq.reset", { id: device.id }, 120000);
          toast(device.name + ": " + (r.message || ""), r.ok === false ? "err" : "");
          await load();
          return;
        }

        const threads = [...picked].sort((a, b) => a - b);
        if (!threads.length) {
          toast(t("irq.chooseAtLeastOne"), "warn");
          return;
        }
        const ok = await confirmBox(device.name, t("irq.confirm") + " " + threads.join(", "), t("irq.bind"), false);
        if (!ok) return;

        const r = await invoke("irq.bind", {
          id: device.id,
          threads,
          priority: pane.querySelector("[data-prio]").checked
        }, 120000);
        toast(device.name + ": " + (r.message || ""), r.ok === false ? "err" : "");
        if (r.ok !== false) toast(t("msg.restartNeeded"), "warn");
        await load();
      });

      body.appendChild(pane);
    }

    await load();
    return { el };
  }
};
