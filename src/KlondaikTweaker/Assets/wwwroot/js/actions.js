import { invoke, on } from "./bridge.js";
import { t } from "./i18n.js";
import { toast, progress, modal, confirmBox, esc, h } from "./ui.js";
import { pulse } from "./scene.js";

let progressOff = null;

function watchProgress() {
  if (progressOff) return;
  progressOff = on("progress", (p) => progress(p.percent));
}

function runPanel(title) {
  const root = document.getElementById("modalRoot");
  const overlay = h(
    '<div class="overlay"><div class="modal stack">' +
      "<h2>" + esc(title) + "</h2>" +
      '<div class="bar"><i style="width:2%"></i></div>' +
      '<div class="small" data-now>' + esc(t("run.starting")) + "</div>" +
      '<div class="runlog" data-log></div>' +
      '<div class="row" style="justify-content:flex-end;margin-top:8px">' +
      '<button class="btn btn-ghost" data-close disabled>' + esc(t("act.close")) + "</button>" +
      "</div></div></div>"
  );

  const fill = overlay.querySelector(".bar > i");
  const now = overlay.querySelector("[data-now]");
  const log = overlay.querySelector("[data-log]");
  const close = overlay.querySelector("[data-close]");
  let finished = false;

  const shut = () => {
    if (!finished) return;
    overlay.remove();
    document.removeEventListener("keydown", onKey);
  };
  const onKey = (e) => {
    if (e.key === "Escape") shut();
  };
  overlay.addEventListener("click", (e) => {
    if (e.target === overlay || e.target.closest("[data-close]")) shut();
  });
  document.addEventListener("keydown", onKey);
  root.appendChild(overlay);

  const line = (text, kind) => {
    const row = h('<div class="runline' + (kind ? " " + kind : "") + '">' + esc(text) + "</div>");
    log.appendChild(row);
    log.scrollTop = log.scrollHeight;
  };

  const off = on("progress", (p) => {
    if (typeof p.percent === "number") fill.style.width = Math.max(2, p.percent) + "%";
    const name = p.title || p.stage || "";
    if (p.phase === "start") {
      now.textContent = name + (p.total ? "  ·  " + (p.done + 1) + " / " + p.total : "");
    } else if (p.phase === "done") {
      line((p.ok === false ? "✗ " : "✓ ") + name + (p.ok === false && p.error ? " — " + p.error : ""), p.ok === false ? "bad" : "");
    }
  });

  return {
    finish(summary, kind) {
      finished = true;
      fill.style.width = "100%";
      now.textContent = summary;
      if (kind) now.classList.add(kind);
      close.disabled = false;
      if (typeof off === "function") off();
    }
  };
}

export async function runTweaks(ids, apply, catalog) {
  if (!ids.length) {
    toast(t("msg.nothingSelected"), "warn");
    return null;
  }

  if (apply && catalog) {
    const extreme = ids.filter((id) => (catalog.get(id) || {}).risk === "extreme");
    if (extreme.length) {
      const list = extreme.map((id) => "<li>" + esc((catalog.get(id) || {}).title || id) + "</li>").join("");
      const res = await modal({
        title: t("msg.confirmExtreme"),
        body:
          '<p class="lead small">' + esc(t("msg.confirmExtremeText")) + "</p>" +
          '<ul class="small dim" style="padding-left:18px">' + list + "</ul>",
        actions: [
          { id: "ok", label: t("act.apply"), danger: true },
          { id: "no", label: t("act.cancel") }
        ]
      });
      if (res !== "ok") return null;
    }
  }

  watchProgress();
  progress(2);
  pulse(4000);

  const panel = runPanel(apply ? t("run.applying") : t("run.reverting"));

  try {
    const r = await invoke(apply ? "tweaks.apply" : "tweaks.revert", { ids });
    progress(100);

    const failed = r.results.filter((x) => !x.ok);
    const okCount = r.results.length - failed.length;
    const summary = (apply ? t("msg.applied") : t("msg.reverted")) + ": " + okCount +
      (failed.length ? "  ·  " + t("msg.failed") + ": " + failed.length : "");
    panel.finish(summary, failed.length ? "bad" : "");

    if (failed.length) {
      toast(summary, "err");
      console.warn("tweak failures", failed);
    } else {
      toast(summary);
    }

    if (r.restart) {
      const yes = await confirmBox(t("msg.restartNeeded"), t("msg.restartNow"), t("act.restart"), true);
      if (yes) invoke("sys.power", { action: "restart" });
    }
    return r;
  } catch (err) {
    progress(100);
    panel.finish(String(err.message || err), "bad");
    toast(String(err.message || err), "err");
    return null;
  }
}

export async function restartExplorer() {
  await invoke("sys.power", { action: "explorer" });
  toast(t("msg.explorerRestart"));
}
