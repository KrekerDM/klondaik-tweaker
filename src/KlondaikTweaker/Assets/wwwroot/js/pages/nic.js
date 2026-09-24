import { invoke } from "../bridge.js";
import { t } from "../i18n.js";
import { h, esc, toast, confirmBox } from "../ui.js";

export default {
  id: "nic",
  icon: "nic",
  group: "system",

  async render() {
    const el = h('<div class="stack" style="gap:20px;max-width:980px"></div>');

    el.appendChild(
      h(
        '<div class="page-head"><div><h1>' + esc(t("nic.title")) + "</h1>" +
          '<p class="lead">' + esc(t("nic.sub")) + "</p></div>" +
          '<div class="row">' + '<button class="btn btn-ghost btn-sm" data-a="refresh">' + esc(t("act.refresh")) + "</button>" + "</div></div>"
      )
    );
    el.appendChild(h('<div class="warn">' + esc(t("nic.warn")) + "</div>"));

    const body = h('<div class="stack"></div>');
    el.appendChild(body);

    let adapters = [];
    let selected = null;

    el.addEventListener("click", (e) => {
      if (e.target.closest('[data-a="refresh"]')) loadAdapters();
    });

    async function loadAdapters() {
      adapters = await invoke("nic.adapters", {}, 120000);
      if (!selected && adapters.length) selected = adapters[0].id;
      await draw();
    }

    async function draw() {
      body.innerHTML = '<div class="stack"><div class="skel"></div><div class="skel"></div></div>';

      if (!adapters.length) {
        body.innerHTML = "";
        body.appendChild(h('<div class="empty">' + esc(t("nic.none")) + "</div>"));
        return;
      }

      const params = await invoke("nic.params", { id: selected }, 120000);
      const adapter = adapters.find((x) => x.id === selected);
      body.innerHTML = "";

      const pick = h('<div class="pane stack"><h2>' + esc(t("nic.adapters")) + '</h2><div class="list"></div></div>');
      const list = pick.querySelector(".list");
      adapters.forEach((a) => {
        list.appendChild(
          h(
            '<div class="item' + (a.id === selected ? " on" : "") + '" data-ad="' + esc(a.id) + '" style="cursor:pointer">' +
              '<div class="grow" style="min-width:0"><div class="name">' + esc(a.name) + "</div></div></div>"
          )
        );
      });
      list.addEventListener("click", async (e) => {
        const row = e.target.closest("[data-ad]");
        if (!row || row.dataset.ad === selected) return;
        selected = row.dataset.ad;
        await draw();
      });
      body.appendChild(pick);

      const edited = params.filter((x) => x.edited).length;
      const pane = h(
        '<div class="pane stack">' +
          "<h2>" + esc(adapter.name) + "</h2>" +
          '<p class="small dim">' + esc(t("nic.count")) + ": " + params.length +
          " · " + esc(t("nic.edited")) + ": " + edited + "</p>" +
          '<div class="row">' +
          '<button class="btn btn-primary btn-sm" data-a="latency">' + esc(t("nic.latency")) + "</button>" +
          '<button class="btn btn-ghost btn-sm" data-a="default">' + esc(t("nic.default")) + "</button>" +
          '<button class="btn btn-ghost btn-sm" data-a="restart">' + esc(t("nic.restart")) + "</button>" +
          "</div>" +
          '<div class="list" data-params></div></div>'
      );
      const rows = pane.querySelector("[data-params]");

      params.forEach((p) => {
        const tags =
          (p.edited ? '<span class="tag applied">' + esc(t("nic.changed")) + "</span> " : "") +
          (p.risky ? '<span class="tag advanced">' + esc(t("nic.risky")) + "</span>" : "");

        const row = h(
          '<div class="item">' +
            '<div class="grow" style="min-width:0">' +
            '<div class="name">' + esc(p.desc) + " " + tags + "</div>" +
            '<div class="sub mono dim">' + esc(p.name) +
            (p.edited ? " · " + esc(t("nic.factory")) + ": " + esc(label(p, p.default)) : "") + "</div>" +
            "</div><div data-ctl></div></div>"
        );

        const ctl = row.querySelector("[data-ctl]");
        if (p.options.length) {
          const sel = h("<select></select>");
          p.options.forEach((o) => {
            const opt = h('<option value="' + esc(o.value) + '">' + esc(o.text) + "</option>");
            if (o.value === p.current) opt.selected = true;
            sel.appendChild(opt);
          });
          sel.addEventListener("change", async () => {
            if (p.risky) {
              const ok = await confirmBox(p.desc, t("nic.confirmRisky"), t("act.confirm"), true);
              if (!ok) {
                sel.value = p.current;
                return;
              }
            }
            await apply(p.name, sel.value, p.desc);
          });
          ctl.appendChild(sel);
        } else {
          const input = h('<input type="text" value="' + esc(p.current) + '" style="width:120px">');
          input.addEventListener("change", async () => await apply(p.name, input.value.trim(), p.desc));
          ctl.appendChild(input);
        }

        rows.appendChild(row);
      });

      async function apply(name, value, desc) {
        const r = await invoke("nic.set", { id: selected, name, value }, 120000);
        toast(desc + ": " + (r.message || ""), r.ok === false ? "err" : "");
        await draw();
      }

      pane.addEventListener("click", async (e) => {
        const btn = e.target.closest("[data-a]");
        if (!btn) return;
        const action = btn.dataset.a;

        if (action === "restart") {
          const r = await invoke("nic.restart", { id: selected }, 180000);
          toast(adapter.name + ": " + (r.message || ""), r.ok === false ? "err" : "");
          return;
        }

        const ok = await confirmBox(
          adapter.name,
          action === "latency" ? t("nic.confirmLatency") : t("nic.confirmDefault"),
          t("act.apply"),
          false
        );
        if (!ok) return;

        const r = await invoke("nic.preset", { id: selected, preset: action }, 300000);
        toast(adapter.name + ": " + (r.message || ""), r.ok === false ? "err" : "");
        await draw();
      });

      body.appendChild(pane);
    }

    function label(p, value) {
      const hit = p.options.find((o) => o.value === value);
      return hit ? hit.text : value;
    }

    await loadAdapters();
    return { el };
  }
};
