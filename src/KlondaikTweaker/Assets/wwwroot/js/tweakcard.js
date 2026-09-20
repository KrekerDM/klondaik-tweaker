import { t } from "./i18n.js";
import { esc, h } from "./ui.js";

function whyUnavailable(item) {
  const note = item.note || "";
  if (note.startsWith("req:")) {
    const req = note.slice(4).trim();
    const named = req
      .split(/[|,+\s]+/)
      .filter(Boolean)
      .map((r) => t("req." + r))
      .filter((x) => x && !x.startsWith("req."));
    if (named.length) return t("tw.whyReq") + ". " + t("tw.needs") + ": " + named.join(", ");
    return t("tw.whyReq");
  }
  if (note === "missing") return t("tw.whyMissing");
  return t("tw.whyMissing");
}

export function tweakCard(item, options = {}) {
  const selectable = options.selectable !== false && item.available !== false;
  const checked = options.checked ? " on" : "";
  const applied = item.state === "applied";
  const off = item.available === false;
  const meta = [];
  if (item.restart) meta.push(t("tw.needRestart"));
  if (item.logoff) meta.push(t("tw.needLogoff"));
  if (item.src) meta.push(t("tw.source") + ": " + item.src);
  meta.push(item.id);

  const el = h(
    '<div class="tw' +
      (applied ? " applied" : "") +
      (off ? " unavail" : "") +
      '" data-id="' +
      esc(item.id) +
      '">' +
      (selectable ? '<div class="cb' + checked + '" data-cb style="margin-top:3px"></div>' : "") +
      '<div class="tw-main">' +
      "<h3>" +
      esc(item.title) +
      '<span class="tag ' +
      esc(item.risk) +
      '">' +
      esc(t("risk." + item.risk)) +
      "</span>" +
      (applied ? '<span class="tag applied">' + esc(t("state.applied")) + "</span>" : "") +
      (item.state === "partial" ? '<span class="tag advanced">' + esc(t("state.partial")) + "</span>" : "") +
      (off ? '<span class="tag unavail">' + esc(t("tw.notSupported")) + "</span>" : "") +
      "</h3>" +
      "<p>" +
      esc(item.desc) +
      "</p>" +
      (off ? '<div class="warn plain">' + esc(whyUnavailable(item)) + "</div>" : "") +
      (item.warn && !off ? '<div class="warn">' + esc(item.warn) + "</div>" : "") +
      '<div class="meta">' +
      meta.map((m) => "<span>" + esc(m) + "</span>").join("") +
      "</div>" +
      "</div>" +
      (off
        ? ""
        : '<div class="sw' +
          (applied ? " on" : "") +
          '" data-sw title="' +
          esc(applied ? t("act.revert") : t("act.apply")) +
          '"></div>') +
      "</div>"
  );

  const cb = el.querySelector("[data-cb]");
  if (cb) cb.addEventListener("click", () => cb.classList.toggle("on"));
  return el;
}

export function setCardState(el, state) {
  const sw = el.querySelector("[data-sw]");
  const applied = state === "applied";
  el.classList.toggle("applied", applied);
  if (sw) {
    sw.classList.toggle("on", applied);
    sw.classList.remove("busy");
    sw.title = applied ? t("act.revert") : t("act.apply");
  }
  const head = el.querySelector("h3");
  if (!head) return;
  head.querySelectorAll(".tag.applied, .tag.advanced.partial").forEach((x) => x.remove());
  if (applied) {
    head.appendChild(h('<span class="tag applied">' + esc(t("state.applied")) + "</span>"));
  }
}
