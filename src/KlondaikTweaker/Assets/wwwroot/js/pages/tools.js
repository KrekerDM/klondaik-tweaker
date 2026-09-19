import { invoke } from "../bridge.js";
import { t, getLang } from "../i18n.js";
import { h, esc, toast, confirmBox, progress } from "../ui.js";
import { mountToolCards, unmountToolCards } from "../toolcards.js";
import { pulse } from "../scene.js";

const TOOLS = [
  {
    id: "services",
    shape: "gear",
    ru: ["Вернуть службы по умолчанию", "232 службы Windows возвращаются к штатным значениям запуска. Помогает, когда другой твикер отключил лишнее и перестали работать звук, сеть или магазин."],
    en: ["Restore default services", "232 Windows services go back to their stock start values. Use it when another tweaker disabled too much and sound, network or the Store stopped working."],
    confirm: true,
    heavy: true
  },
  {
    id: "defender",
    shape: "shield",
    ru: ["Вернуть Защитник", "Снимает политики отключения антивируса и брандмауэра и включает обратно их службы."],
    en: ["Restore Defender", "Removes the policies that disable the antivirus and firewall and re-enables their services."],
    confirm: false
  },
  {
    id: "updates",
    shape: "refresh",
    ru: ["Вернуть обновления", "Убирает все политики Центра обновления и включает службы обновления, доставки и восстановления."],
    en: ["Restore updates", "Clears every Windows Update policy and re-enables the update, delivery and medic services."],
    confirm: false
  },
  {
    id: "store",
    shape: "box",
    ru: ["Починить Microsoft Store", "Сбрасывает кэш магазина, включает его службы и перерегистрирует пакет. Лечит бесконечную загрузку и ошибки установки."],
    en: ["Repair Microsoft Store", "Resets the Store cache, re-enables its services and re-registers the package."],
    confirm: false,
    heavy: true
  },
  {
    id: "search",
    shape: "search",
    ru: ["Перестроить поиск", "Сбрасывает индекс Windows Search и запускает индексацию заново. Лечит поиск, который ничего не находит."],
    en: ["Rebuild search index", "Resets the Windows Search index and starts indexing again."],
    confirm: false
  },
  {
    id: "network",
    shape: "globe",
    ru: ["Сбросить сеть", "Winsock, IP, DNS и ARP возвращаются к исходному состоянию, сетевые службы включаются, блокировка доменов снимается."],
    en: ["Reset network", "Winsock, IP, DNS and ARP go back to defaults, network services are re-enabled and host blocks are lifted."],
    confirm: true,
    restart: true
  },
  {
    id: "memory",
    shape: "memory",
    ru: ["Очистить память", "Сбрасывает рабочие наборы процессов и список ожидания. Свободная память возвращается без перезапуска программ."],
    en: ["Trim memory", "Drops process working sets and the standby list. Free memory returns without restarting anything."],
    confirm: false
  },
  {
    id: "dns",
    shape: "knot",
    ru: ["Очистить кэш DNS", "Сбрасывает сохранённые адреса сайтов. Помогает, когда сайт переехал, а браузер стучится на старый адрес."],
    en: ["Flush DNS cache", "Clears cached site addresses. Helps when a site moved but the browser still hits the old address."],
    confirm: false
  },
  {
    id: "godmode",
    shape: "god",
    ru: ["GodMode", "Открывает скрытую панель со всеми настройками Windows в одном списке — больше двухсот пунктов."],
    en: ["GodMode", "Opens the hidden panel with every Windows setting in a single list."],
    confirm: false
  },
  {
    id: "explorer",
    shape: "spark",
    ru: ["Перезапустить проводник", "Перезапускает explorer.exe. Применяет твики интерфейса без перезахода в систему."],
    en: ["Restart Explorer", "Restarts explorer.exe so interface tweaks apply without signing out."],
    confirm: false
  }
];

export default {
  id: "tools",
  icon: "tools",
  group: "main",

  async render() {
    const lang = getLang();
    const el = h('<div class="stack" style="gap:20px"></div>');

    el.appendChild(
      h(
        '<div class="page-head"><div><h1>' + esc(t("tools.title")) + "</h1>" +
          '<p class="lead">' + esc(t("tools.sub")) + "</p></div></div>"
      )
    );

    const grid = h('<div class="toolgrid"></div>');
    TOOLS.forEach((tool) => {
      const text = lang === "en" ? tool.en : tool.ru;
      grid.appendChild(
        h(
          '<button class="toolcard" data-tool="' + esc(tool.id) + '">' +
            '<div class="toolcard-3d" data-shape="' + esc(tool.shape) + '"></div>' +
            '<div class="toolcard-body">' +
            "<h3>" + esc(text[0]) + "</h3>" +
            "<p>" + esc(text[1]) + "</p>" +
            '<div class="toolcard-state" data-state></div>' +
            "</div></button>"
        )
      );
    });
    el.appendChild(grid);

    grid.addEventListener("click", async (e) => {
      const card = e.target.closest("[data-tool]");
      if (!card || card.classList.contains("busy")) return;
      const tool = TOOLS.find((x) => x.id === card.dataset.tool);
      if (!tool) return;
      const text = getLang() === "en" ? tool.en : tool.ru;

      if (tool.confirm) {
        const ok = await confirmBox(text[0], text[1], t("act.run"), true);
        if (!ok) return;
      }

      const state = card.querySelector("[data-state]");
      card.classList.add("busy");
      state.textContent = t("tools.working");
      if (tool.heavy) progress(10);

      try {
        const r = await invoke("repair.run", { id: tool.id }, 1200000);
        state.textContent = r.message || t("act.finish");
        card.classList.toggle("failed", r.ok === false);
        toast(text[0] + ": " + (r.message || t("act.finish")), r.ok === false ? "err" : "");
        if (r.ok !== false) pulse(2200);
        if (tool.restart) toast(t("msg.restartNeeded"), "warn");
      } catch (err) {
        state.textContent = String(err.message || err);
        card.classList.add("failed");
        toast(String(err.message || err), "err");
      } finally {
        card.classList.remove("busy");
        if (tool.heavy) progress(100);
      }
    });

    setTimeout(() => mountToolCards(grid), 30);
    return { el, dispose: unmountToolCards };
  }
};
