import { invoke } from "../bridge.js";
import { t } from "../i18n.js";
import { h, esc, toast } from "../ui.js";

const MODES = ["auto", "delayed", "manual", "disabled"];

export default {
  id: "services",
  icon: "services",
  group: "system",

  async render() {
    const el = h('<div class="stack" style="gap:20px"></div>');
    const state = { q: "", running: false, recommended: false };
    let all = [];

    el.appendChild(
      h(
        '<div class="page-head"><div><h1>' + esc(t("svc.title")) + '</h1>' +
          '<p class="lead">' + esc(t("svc.sub")) + "</p></div>" +
          '<input type="search" id="svcq" placeholder="' + esc(t("svc.search")) + '" style="min-width:260px"></div>'
      )
    );

    const filters = h(
      '<div class="row">' +
        '<button class="chip" data-f="running">' + esc(t("svc.onlyRunning")) + "</button>" +
        '<button class="chip" data-f="recommended">' + esc(t("svc.recommended")) + "</button>" +
        '<span class="small dim" data-count></span>' +
        "</div>"
    );
    el.appendChild(filters);

    const list = h('<div class="list"></div>');
    el.appendChild(list);

    function draw() {
      const q = state.q.toLowerCase();
      const items = all.filter((s) => {
        if (state.running && s.status !== "running") return false;
        if (state.recommended && !s.recommended) return false;
        if (!q) return true;
        return (
          s.name.toLowerCase().includes(q) ||
          (s.display || "").toLowerCase().includes(q) ||
          (s.desc || "").toLowerCase().includes(q)
        );
      });

      filters.querySelector("[data-count]").textContent = items.length + " / " + all.length;
      list.innerHTML = "";
      if (!items.length) {
        list.appendChild(h('<div class="empty">' + esc(t("msg.empty")) + "</div>"));
        return;
      }

      items.slice(0, 400).forEach((s) => {
        const row = h(
          '<div class="item" data-name="' + esc(s.name) + '">' +
            '<div class="grow" style="min-width:0"><div class="name">' + esc(s.display) +
            (s.recommended ? ' <span class="tag safe">' + esc(t("svc.recommended")) + "</span>" : "") +
            (s.touched ? ' <span class="tag applied">' + esc(t("state.applied")) + "</span>" : "") +
            '</div><div class="sub">' + esc(s.desc || s.name) + "</div></div>" +
            '<span class="tag ' + (s.status === "running" ? "safe" : "plain") + '">' +
            esc(s.status === "running" ? t("svc.running") : t("svc.stopped")) + "</span>" +
            '<select data-mode>' +
            MODES.map((m) => '<option value="' + m + '"' + (s.start === m ? " selected" : "") + ">" + esc(t("svc.mode." + m)) + "</option>").join("") +
            "</select>" +
            '<button class="btn btn-ghost btn-sm" data-ctl="' + (s.status === "running" ? "stop" : "start") + '">' +
            esc(s.status === "running" ? t("act.stop") : t("act.start")) + "</button>" +
            "</div>"
        );
        list.appendChild(row);
      });
      if (items.length > 400) list.appendChild(h('<div class="empty small">+' + (items.length - 400) + "</div>"));
    }

    list.addEventListener("change", async (e) => {
      const sel = e.target.closest("[data-mode]");
      if (!sel) return;
      const name = sel.closest(".item").dataset.name;
      sel.disabled = true;
      try {
        const r = await invoke("services.set", { name, mode: sel.value });
        if (r.ok) {
          const s = all.find((x) => x.name === name);
          if (s) {
            s.start = r.start;
            s.status = r.status;
            s.touched = true;
          }
          toast(name + " · " + t("svc.mode." + sel.value));
        } else {
          toast(t("msg.failed") + ": " + (r.error || name), "err");
        }
      } finally {
        sel.disabled = false;
      }
    });

    list.addEventListener("click", async (e) => {
      const btn = e.target.closest("[data-ctl]");
      if (!btn) return;
      const row = btn.closest(".item");
      const name = row.dataset.name;
      btn.disabled = true;
      try {
        const r = await invoke("services.control", { name, action: btn.dataset.ctl });
        const s = all.find((x) => x.name === name);
        if (s) s.status = r.status;
        if (!r.ok) toast(t("msg.failed") + ": " + name, "err");
        draw();
      } finally {
        btn.disabled = false;
      }
    });

    filters.addEventListener("click", (e) => {
      const chip = e.target.closest("[data-f]");
      if (!chip) return;
      state[chip.dataset.f] = !state[chip.dataset.f];
      chip.classList.toggle("on", state[chip.dataset.f]);
      draw();
    });

    let debounce = 0;
    el.querySelector("#svcq").addEventListener("input", (e) => {
      state.q = e.target.value;
      clearTimeout(debounce);
      debounce = setTimeout(draw, 180);
    });

    list.innerHTML = '<div class="skel"></div><div class="skel"></div><div class="skel"></div>';
    all = await invoke("services.list");
    draw();
    return { el };
  }
};
