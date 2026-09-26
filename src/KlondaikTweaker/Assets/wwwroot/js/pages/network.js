import { invoke } from "../bridge.js";
import { t, textLang } from "../i18n.js";
import { h, esc, toast, switchEl, confirmBox } from "../ui.js";

export default {
  id: "network",
  icon: "network",
  group: "system",

  async render() {
    const el = h('<div class="stack" style="gap:20px"></div>');
    el.appendChild(
      h(
        '<div class="page-head"><div><h1>' + esc(t("net.title")) + '</h1>' +
          '<p class="lead">' + esc(t("net.sub")) + "</p></div>" +
          '<button class="btn btn-ghost" data-a="refresh">' + esc(t("act.refresh")) + "</button></div>"
      )
    );

    const adapters = h('<div class="pane stack"><h2>' + esc(t("net.adapters")) + '</h2><div class="list" data-ad></div></div>');
    el.appendChild(adapters);

    const dnsBox = h(
      '<div class="pane stack"><div class="row"><h2 class="grow">' + esc(t("net.dns")) + "</h2>" +
        '<button class="btn btn-ghost btn-sm" data-a="testdns">' + esc(t("net.testDns")) + "</button></div>" +
        '<div class="grid g3" data-dns></div>' +
        '<div class="row"><select data-target style="min-width:220px"></select>' +
        '<button class="btn btn-primary btn-sm" data-a="applydns">' + esc(t("net.apply")) + "</button>" +
        '<button class="btn btn-ghost btn-sm" data-a="autodns">' + esc(t("net.auto")) + "</button></div></div>"
    );
    el.appendChild(dnsBox);

    const tools = h(
      '<div class="grid g2">' +
        '<div class="pane stack"><div class="row"><h2 class="grow">' + esc(t("net.ping")) + "</h2>" +
        '<button class="btn btn-ghost btn-sm" data-a="ping">' + esc(t("net.testPing")) + "</button></div>" +
        '<div class="list" data-ping><div class="empty small">-</div></div></div>' +
        '<div class="pane stack"><h2>' + esc(t("page.tweaks")) + "</h2>" +
        '<div class="list">' +
        '<div class="item"><div class="grow"><div class="name">' + esc(t("net.nagle")) + '</div>' +
        '<div class="sub">' + esc(t("net.nagleHint")) + "</div></div><div data-nagle></div></div>" +
        '<div class="item"><div class="grow"><div class="name">' + esc(t("net.telemetry")) + '</div>' +
        '<div class="sub" data-hosts></div></div><div data-tele></div></div>' +
        '<div class="item"><div class="grow"><div class="name">' + esc(t("net.reset")) + '</div>' +
        '<div class="sub">' + esc(t("net.resetHint")) + "</div></div>" +
        '<button class="btn btn-ghost btn-sm" data-a="reset">' + esc(t("act.run")) + "</button></div>" +
        "</div></div></div>"
    );
    el.appendChild(tools);

    const tcpBox = h('<div class="pane stack"><h2>TCP</h2><div class="spec" data-tcp></div></div>');
    el.appendChild(tcpBox);

    let selectedDns = null;

    async function loadTcp() {
      const box = tcpBox.querySelector("[data-tcp]");
      box.innerHTML = '<div><dt>...</dt><dd>-</dd></div>';
      try {
        const tcp = await invoke("net.tcp", {}, 120000);
        box.innerHTML = Object.keys(tcp)
          .filter((k) => tcp[k])
          .map((k) => "<div><dt>" + esc(k) + "</dt><dd>" + esc(tcp[k]) + "</dd></div>")
          .join("") || '<div><dt>-</dt><dd>-</dd></div>';
      } catch {
        box.innerHTML = '<div><dt>-</dt><dd>-</dd></div>';
      }
    }

    async function load() {
      const data = await invoke("net.adapters");
      const box = adapters.querySelector("[data-ad]");
      const target = dnsBox.querySelector("[data-target]");
      box.innerHTML = "";
      target.innerHTML = "";

      data.adapters.forEach((a) => {
        box.appendChild(
          h(
            '<div class="item"><div class="grow" style="min-width:0">' +
              '<div class="name">' + esc(a.name) + (a.up ? ' <span class="tag safe">up</span>' : ' <span class="tag plain">down</span>') + "</div>" +
              '<div class="sub mono">' + esc(a.desc) + "</div></div>" +
              '<div class="mono small" style="text-align:right">' + esc(a.ip || "-") +
              '<div class="dim">' + esc((a.dns || []).join(", ") || "-") + "</div></div>" +
              (a.speed ? '<span class="tag plain mono">' + a.speed + " Mb</span>" : "") +
              "</div>"
          )
        );
        if (a.up) target.appendChild(h('<option value="' + esc(a.name) + '">' + esc(a.name) + "</option>"));
      });

      tools.querySelector("[data-hosts]").textContent = data.hosts + " " + t("net.blocked");

      const nagleBox = tools.querySelector("[data-nagle]");
      nagleBox.innerHTML = "";
      const nagleOn = data.adapters.some((a) => a.nagle);
      nagleBox.appendChild(
        switchEl(nagleOn, async (next) => {
          await invoke("net.nagle", { disabled: next });
          toast(next ? t("act.apply") : t("act.revert"));
          return true;
        })
      );

      const teleBox = tools.querySelector("[data-tele]");
      teleBox.innerHTML = "";
      teleBox.appendChild(
        switchEl(data.telemetry, async (next) => {
          const r = await invoke("net.telemetry", { block: next });
          tools.querySelector("[data-hosts]").textContent = r.count + " " + t("net.blocked");
          return true;
        })
      );
    }

    function drawDns(list) {
      const box = dnsBox.querySelector("[data-dns]");
      box.innerHTML = "";
      list.forEach((d) => {
        const card = h(
          '<button class="opt' + (selectedDns === d.id ? " on" : "") + '" data-dnsid="' + esc(d.id) + '">' +
            '<div style="min-width:0"><div class="t">' + esc(d.name) +
            (d.ping >= 0 ? ' <span class="mono dim small">' + d.ping + " " + esc(t("unit.ms")) + "</span>" : "") +
            '</div><div class="h">' + esc(textLang() === "en" ? d.noteEn : d.noteRu) + "</div>" +
            '<div class="h mono">' + esc(d.primary) + " · " + esc(d.secondary) + "</div></div></button>"
        );
        card.addEventListener("click", () => {
          selectedDns = d.id;
          box.querySelectorAll(".opt").forEach((x) => x.classList.toggle("on", x.dataset.dnsid === d.id));
        });
        box.appendChild(card);
      });
      box.dataset.list = JSON.stringify(list.map((x) => ({ id: x.id, p: x.primary, s: x.secondary })));
    }

    el.addEventListener("click", async (e) => {
      const btn = e.target.closest("[data-a]");
      if (!btn) return;
      btn.disabled = true;
      try {
        const action = btn.dataset.a;
        if (action === "refresh") await load();
        else if (action === "testdns") {
          const list = await invoke("net.testDns");
          drawDns(list);
          toast(t("net.testDns") + ": " + t("act.finish"));
        } else if (action === "ping") {
          const box = tools.querySelector("[data-ping]");
          box.innerHTML = '<div class="skel" style="height:40px"></div>';
          const res = await invoke("net.ping");
          box.innerHTML = "";
          res.forEach((r) => {
            box.appendChild(
              h(
                '<div class="item"><div class="grow"><div class="name">' + esc(r.name) + "</div>" +
                  '<div class="sub mono">' + esc(r.host) + "</div></div>" +
                  '<div class="mono">' + (r.ping >= 0 ? r.ping + " " + esc(t("unit.ms")) : "-") + "</div>" +
                  (r.loss > 0 ? '<span class="tag extreme">' + r.loss + "% loss</span>" : "") +
                  "</div>"
              )
            );
          });
        } else if (action === "applydns" || action === "autodns") {
          const adapter = dnsBox.querySelector("[data-target]").value;
          if (!adapter) return toast(t("msg.nothingSelected"), "warn");
          let primary = "";
          let secondary = "";
          if (action === "applydns") {
            const raw = dnsBox.querySelector("[data-dns]").dataset.list;
            const list = raw ? JSON.parse(raw) : [];
            const pick = list.find((x) => x.id === selectedDns);
            if (!pick) return toast(t("msg.nothingSelected"), "warn");
            primary = pick.p;
            secondary = pick.s;
          }
          const r = await invoke("net.setDns", { adapter, primary, secondary });
          toast(r.result === "ok" ? t("act.apply") : r.result, r.result === "ok" ? "" : "err");
          await load();
        } else if (action === "reset") {
          const ok = await confirmBox(t("net.reset"), t("net.resetHint"), t("act.run"), true);
          if (!ok) return;
          const r = await invoke("net.reset");
          toast(r.result, r.result === "ok" ? "" : "err");
        }
      } catch (err) {
        toast(String(err.message || err), "err");
      } finally {
        btn.disabled = false;
      }
    });

    drawDns(await invoke("net.dns"));
    await load();
    loadTcp();
    return { el };
  }
};
