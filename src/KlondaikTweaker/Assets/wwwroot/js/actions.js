import { invoke, on } from "./bridge.js";
import { t } from "./i18n.js";
import { toast, progress, modal, confirmBox, esc } from "./ui.js";
import { pulse } from "./scene.js";

let progressOff = null;

function watchProgress() {
  if (progressOff) return;
  progressOff = on("progress", (p) => progress(p.percent));
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

  try {
    const r = await invoke(apply ? "tweaks.apply" : "tweaks.revert", { ids });
    progress(100);

    const failed = r.results.filter((x) => !x.ok);
    const okCount = r.results.length - failed.length;
    if (failed.length) {
      toast(
        (apply ? t("msg.applied") : t("msg.reverted")) + ": " + okCount + " · " + t("msg.failed") + ": " + failed.length,
        "err"
      );
      console.warn("tweak failures", failed);
    } else {
      toast((apply ? t("msg.applied") : t("msg.reverted")) + ": " + okCount);
    }

    if (r.restart) {
      const yes = await confirmBox(t("msg.restartNeeded"), t("msg.restartNow"), t("act.restart"), true);
      if (yes) invoke("sys.power", { action: "restart" });
    }
    return r;
  } catch (err) {
    progress(100);
    toast(String(err.message || err), "err");
    return null;
  }
}

export async function restartExplorer() {
  await invoke("sys.power", { action: "explorer" });
  toast(t("msg.explorerRestart"));
}
