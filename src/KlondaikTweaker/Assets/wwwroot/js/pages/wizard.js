import { invoke } from "../bridge.js";
import { t } from "../i18n.js";
import { h, esc, toast } from "../ui.js";
import { tweakCard } from "../tweakcard.js";
import { runTweaks } from "../actions.js";

export default {
  id: "wizard",
  icon: "wizard",
  group: "main",

  async render(app) {
    const el = h('<div class="stack" style="gap:20px;max-width:900px"></div>');
    const answers = {};
    let questions = [];
    let index = 0;

    const head = h(
      '<div><h1>' + esc(t("wiz.title")) + '</h1><p class="lead">' + esc(t("wiz.sub")) + "</p></div>"
    );
    el.appendChild(head);

    const body = h('<div class="stack"></div>');
    el.appendChild(body);

    function visible() {
      return questions.filter((q) => matches(q.when, answers));
    }

    function intro() {
      const f = app.info.facts;
      const facts = [
        f.laptop ? "Laptop" : "Desktop",
        f.systemSsd ? "SSD" : "HDD",
        f.nvidia ? "NVIDIA" : f.amd ? "AMD" : "Intel",
        f.isWin11 ? "Windows 11" : "Windows 10",
        f.ramGb + " " + t("unit.gb"),
        f.cores + "C/" + f.threads + "T"
      ];
      body.innerHTML = "";
      body.appendChild(
        h(
          '<div class="pane stack">' +
            '<div class="label small dim" style="letter-spacing:.09em;text-transform:uppercase">' +
            esc(t("wiz.detected")) +
            "</div>" +
            '<div class="row">' +
            facts.map((x) => '<span class="tag plain">' + esc(x) + "</span>").join("") +
            "</div>" +
            '<p class="small dim">' + esc(f.cpu) + " · " + esc((f.gpus && f.gpus[0]) || "") + "</p>" +
            '<div class="row" style="margin-top:8px"><button class="btn btn-primary btn-lg" data-go="start">' +
            esc(t("wiz.begin")) +
            "</button></div>" +
            "</div>"
        )
      );
    }

    function steps(total, current) {
      return (
        '<div class="steps">' +
        Array.from({ length: total }, (_, i) => '<i class="' + (i < current ? "done" : i === current ? "now" : "") + '"></i>').join("") +
        "</div>"
      );
    }

    function question() {
      const list = visible();
      if (index >= list.length) return result();
      const q = list[index];
      body.innerHTML = "";
      const card = h(
        '<div class="pane stack wizard-q">' +
          steps(list.length, index) +
          '<div class="small dim mono">' + (index + 1) + " " + esc(t("wiz.of")) + " " + list.length + "</div>" +
          "<h2>" + esc(q.title) + "</h2>" +
          (q.sub ? '<p class="small dim">' + esc(q.sub) + "</p>" : "") +
          '<div class="stack-sm" data-opts></div>' +
          '<div class="row" style="margin-top:12px">' +
          '<button class="btn btn-ghost" data-go="back"' + (index === 0 ? " disabled" : "") + ">" + esc(t("act.back")) + "</button>" +
          '<div class="grow"></div>' +
          '<button class="btn btn-primary" data-go="next">' + esc(t("act.next")) + "</button>" +
          "</div>" +
          "</div>"
      );

      const opts = card.querySelector("[data-opts]");
      const picked = answers[q.id] || [];
      q.options.forEach((o) => {
        const btn = h(
          '<button class="opt' + (picked.includes(o.id) ? " on" : "") + '" data-opt="' + esc(o.id) + '">' +
            '<div class="cb' + (picked.includes(o.id) ? " on" : "") + '" style="margin-top:2px;pointer-events:none"></div>' +
            '<div><div class="t">' + esc(o.title) + "</div>" +
            (o.hint ? '<div class="h">' + esc(o.hint) + "</div>" : "") +
            "</div></button>"
        );
        btn.addEventListener("click", () => {
          const current = answers[q.id] || [];
          if (q.multi) {
            answers[q.id] = current.includes(o.id) ? current.filter((x) => x !== o.id) : current.concat(o.id);
          } else {
            answers[q.id] = [o.id];
          }
          opts.querySelectorAll(".opt").forEach((x) => {
            const on = (answers[q.id] || []).includes(x.dataset.opt);
            x.classList.toggle("on", on);
            x.querySelector(".cb").classList.toggle("on", on);
          });
          if (!q.multi) setTimeout(next, 160);
        });
        opts.appendChild(btn);
      });

      body.appendChild(card);
    }

    function next() {
      const list = visible();
      const q = list[index];
      if (q && !(answers[q.id] || []).length) {
        toast(t("msg.nothingSelected"), "warn");
        return;
      }
      index++;
      question();
    }

    async function result() {
      body.innerHTML = '<div class="skel"></div><div class="skel"></div>';
      const data = await invoke("wizard.resolve", { answers });
      body.innerHTML = "";

      if (!data.items.length) {
        body.appendChild(h('<div class="pane empty">' + esc(t("wiz.nothing")) + "</div>"));
        return;
      }

      const catalog = new Map(data.items.map((x) => [x.id, x]));
      const summary = h(
        '<div class="pane stack">' +
          "<h2>" + esc(t("wiz.result")) + "</h2>" +
          '<p class="small dim">' + esc(t("wiz.resultSub")) + "</p>" +
          '<div class="row">' +
          '<span class="tag safe">' + esc(t("risk.safe")) + ": " + data.counts.safe + "</span>" +
          '<span class="tag advanced">' + esc(t("risk.advanced")) + ": " + data.counts.advanced + "</span>" +
          (data.counts.extreme ? '<span class="tag extreme">' + esc(t("risk.extreme")) + ": " + data.counts.extreme + "</span>" : "") +
          '<span class="tag applied">' + esc(t("state.applied")) + ": " + data.counts.already + "</span>" +
          "</div></div>"
      );
      body.appendChild(summary);

      const list = h('<div class="stack"></div>');
      data.items.forEach((item) => list.appendChild(tweakCard(item, { checked: item.state !== "applied" })));
      body.appendChild(list);

      const bar = h(
        '<div class="sticky-actions">' +
          '<button class="btn btn-ghost btn-sm" data-go="again">' + esc(t("wiz.again")) + "</button>" +
          '<div class="grow"></div>' +
          '<button class="btn btn-ghost btn-sm" data-sel="all">' + esc(t("act.selectAll")) + "</button>" +
          '<button class="btn btn-ghost btn-sm" data-sel="none">' + esc(t("act.clear")) + "</button>" +
          '<button class="btn btn-primary" data-go="apply">' + esc(t("wiz.applySelected")) + "</button>" +
          "</div>"
      );
      body.appendChild(bar);

      bar.addEventListener("click", async (e) => {
        const sel = e.target.closest("[data-sel]");
        if (sel) {
          const on = sel.dataset.sel === "all";
          list.querySelectorAll(".cb").forEach((cb) => cb.classList.toggle("on", on));
          return;
        }
        const go = e.target.closest("[data-go]");
        if (!go) return;
        if (go.dataset.go === "again") {
          index = 0;
          for (const k of Object.keys(answers)) delete answers[k];
          question();
          return;
        }
        const ids = [...list.querySelectorAll(".tw")].filter((x) => x.querySelector(".cb.on")).map((x) => x.dataset.id);
        const r = await runTweaks(ids, true, catalog);
        if (r) {
          await invoke("app.setSetting", { key: "wizardDone", value: true });
          await app.refresh();
          result();
        }
      });
    }

    body.addEventListener("click", (e) => {
      const go = e.target.closest("[data-go]");
      if (!go) return;
      if (go.dataset.go === "start") {
        index = 0;
        question();
      } else if (go.dataset.go === "next") {
        next();
      } else if (go.dataset.go === "back") {
        index = Math.max(0, index - 1);
        question();
      }
    });

    questions = await invoke("wizard.questions");
    intro();
    return { el };
  }
};

function matches(when, answers) {
  if (!when) return true;
  return when.split("&").every((clause) => {
    const neg = clause.includes("!=");
    const [key, value] = clause.split(neg ? "!=" : "=").map((x) => x.trim());
    const picked = answers[key] || [];
    const has = picked.includes(value);
    return neg ? !has : has;
  });
}
