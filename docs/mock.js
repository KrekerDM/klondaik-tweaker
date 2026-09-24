(function () {
  const listeners = new Set();
  const facts = {
    osName: "Windows 11 Pro",
    osVersion: "11 24H2 (build 26200.1742)",
    build: 26200,
    edition: "Windows 11 Pro",
    isWin11: true,
    cpu: "Intel(R) Core(TM) i7-14700KF",
    cores: 20,
    threads: 28,
    ramGb: 63.8,
    gpus: ["NVIDIA GeForce RTX 4070 Ti"],
    nvidia: true,
    amd: false,
    intel: false,
    laptop: false,
    systemSsd: true,
    motherboard: "Gigabyte B760 AORUS ELITE",
    host: "AORUS",
    user: "Aorus",
    powerPlan: "Сбалансированная",
    defenderReal: true,
    tamperProtection: true,
    vbsRunning: true,
    secureBoot: true,
    arch: "x64",
    tier: "weak"
  };

  const settings = {
    lang: "ru",
    showExtreme: true,
    autoRestorePoint: true,
    monitor3d: true,
    liveMonitor: true,
    reduced: false,
    wizardDone: true,
    acceptedRisk: false,
    accent: "ice",
    favorites: []
  };

  let tweaks = null;
  let creditsDb = { note: {}, sources: [] };
  let wizard = null;
  let software = null;

  async function data(name) {
    const r = await fetch("data/" + name + ".json");
    return r.json();
  }

  const FACT_OK = {
    win11: true, win10: false, laptop: false, desktop: true, ssd: true, hdd: false,
    nvidia: true, amd: false, intel: false, b22h2: true, b24h2: true,
    lowram: false, highram: true, weakcpu: false, weak: false, strong: true
  };

  function meets(req) {
    if (!req) return true;
    return req.split(/[|,+\s]+/).filter(Boolean).every((r) => FACT_OK[r] === true);
  }

  function view(t) {
    const applied = ["ui.file-extensions", "priv.telemetry-off", "game.gamedvr-off"].includes(t.id);
    const ok = meets(t.req);
    const L = t[settings.lang] || t.ru;
    return {
      id: t.id,
      cat: t.cat,
      risk: t.risk,
      title: L.t,
      desc: L.d,
      warn: L.w,
      tags: t.tags || [],
      src: t.src,
      restart: !!t.restart,
      logoff: !!t.logoff,
      state: ok ? (applied ? "applied" : "notapplied") : "unavailable",
      available: ok,
      note: ok ? null : "req:" + t.req
    };
  }

  const handlers = {
    "app.info": () => ({
      version: "1.0.0",
      settings,
      facts,
      restore: true,
      tweakCount: tweaks.tweaks.length,
      journal: 3,
      boost: false,
      dataDir: "C:\\ProgramData\\KlondaikTweaker",
      releasesPage: "https://github.com/KrekerDM/klondaik-tweaker/releases"
    }),
    "app.setSetting": (p) => {
      settings[p.key] = p.value;
      return settings;
    },
    "tweaks.list": (p) => {
      const items = tweaks.tweaks
        .filter((t) => (!p.cat || p.cat === "all" ? true : t.cat === p.cat))
        .filter((t) => (!p.risk || p.risk === "all" ? true : t.risk === p.risk))
        .filter((t) => (!p.q ? true : ((t[settings.lang] || t.ru).t + (t[settings.lang] || t.ru).d + t.id).toLowerCase().includes(p.q.toLowerCase())))
        .map(view);
      const cats = {};
      const risks = {};
      tweaks.tweaks.forEach((t) => {
        cats[t.cat] = (cats[t.cat] || 0) + 1;
        risks[t.risk] = (risks[t.risk] || 0) + 1;
      });
      return { items, cats, risks, total: tweaks.tweaks.length };
    },
    "tweaks.presets": () => {
      const ru = [
        ["Сбалансированный", "Только безопасные твики"],
        ["Игровой", "FPS, задержки, сеть"],
        ["Приватность", "Телеметрия и реклама"],
        ["Максимум", "Всё, включая агрессивное"]
      ];
      const en = [
        ["Balanced", "Safe tweaks only"],
        ["Gaming", "FPS, latency, network"],
        ["Privacy", "Telemetry and ads"],
        ["Maximum", "Everything, aggressive included"]
      ];
      const rows = settings.lang === "en" ? en : ru;
      return [
        { id: "balanced", title: rows[0][0], desc: rows[0][1], count: 64, ids: [] },
        { id: "gaming", title: rows[1][0], desc: rows[1][1], count: 48, ids: [] },
        { id: "privacy", title: rows[2][0], desc: rows[2][1], count: 17, ids: [] },
        { id: "max", title: rows[3][0], desc: rows[3][1], count: 114, ids: [] }
      ];
    },
    "tweaks.apply": (p) => ({
      results: p.ids.map((id) => ({ id, ok: true, error: null, state: "applied" })),
      restart: false,
      restorePoint: "ok",
      applied: p.ids.length
    }),
    "tweaks.revert": (p) => ({
      results: p.ids.map((id) => ({ id, ok: true, error: null, state: "notapplied" })),
      restart: false,
      applied: p.ids.length
    }),
    "wizard.questions": () =>
      wizard.questions.map((q) => ({
        id: q.id,
        multi: q.multi,
        title: settings.lang === "en" ? q.en : q.ru,
        sub: settings.lang === "en" ? q.sen : q.sru,
        when: q.when,
        options: q.options.map((o) => ({
          id: o.id,
          title: settings.lang === "en" ? o.en : o.ru,
          hint: settings.lang === "en" ? o.hen : o.hru
        }))
      })),
    "wizard.resolve": () => {
      const items = tweaks.tweaks.slice(0, 26).map(view);
      return {
        items,
        counts: {
          safe: items.filter((x) => x.risk === "safe").length,
          advanced: items.filter((x) => x.risk === "advanced").length,
          extreme: items.filter((x) => x.risk === "extreme").length,
          already: items.filter((x) => x.state === "applied").length
        }
      };
    },
    "restore.status": () => ({
      enabled: true,
      points: [
        { seq: 42, desc: "Klondaik Tweaker: 18.09 19:04", time: "2026-09-18 19:04" },
        { seq: 41, desc: "Установка драйвера NVIDIA", time: "2026-09-16 11:20" }
      ]
    }),
    "restore.create": () => ({ ok: true, message: "ok" }),
    "clean.scan": () => ({
      targets: [
        { id: "temp.user", ru: "Временные файлы пользователя", en: "User temp", descRu: "Папка %TEMP% текущего пользователя", descEn: "", bytes: 3128472913, files: 18422, safe: true, default: true },
        { id: "temp.win", ru: "Временные файлы Windows", en: "Windows temp", descRu: "C:\\Windows\\Temp", descEn: "", bytes: 412398123, files: 2104, safe: true, default: true },
        { id: "wu.cache", ru: "Кэш обновлений Windows", en: "Update cache", descRu: "Скачанные пакеты обновлений", descEn: "", bytes: 2847362819, files: 431, safe: true, default: true },
        { id: "shader", ru: "Кэш шейдеров", en: "Shader cache", descRu: "DirectX, NVIDIA, AMD и Intel", descEn: "", bytes: 1938472910, files: 8921, safe: true, default: true },
        { id: "browsers", ru: "Кэш браузеров", en: "Browser caches", descRu: "Chrome, Edge, Brave, Firefox", descEn: "", bytes: 4728391028, files: 31204, safe: true, default: true },
        { id: "prefetch", ru: "Prefetch", en: "Prefetch", descRu: "Данные ускорения запуска", descEn: "", bytes: 24839201, files: 312, safe: false, default: false },
        { id: "recycle", ru: "Корзина", en: "Recycle Bin", descRu: "Полная очистка корзины", descEn: "", bytes: 8472910283, files: 1204, safe: true, default: false }
      ],
      disks: {
        drives: [
          { name: "C:\\", label: "System", total: 2000398934016, free: 421398934016, format: "NTFS" },
          { name: "D:\\", label: "Games", total: 4000797868032, free: 1821398934016, format: "NTFS" }
        ]
      }
    }),
    "clean.run": () => ({ freed: 12938471028, files: 61594, errors: [] }),
    "startup.list": () => [
      { id: "a", name: "Steam", command: "\"C:\\Program Files (x86)\\Steam\\steam.exe\" -silent", source: "HKCU Run", kind: "reg", enabled: true },
      { id: "b", name: "Discord", command: "C:\\Users\\Aorus\\AppData\\Local\\Discord\\Update.exe --processStart Discord.exe", source: "HKCU Run", kind: "reg", enabled: true },
      { id: "c", name: "NVIDIA App", command: "C:\\Program Files\\NVIDIA Corporation\\NVIDIA App\\CEF\\NVIDIA app.exe -silent", source: "HKLM Run", kind: "reg", enabled: false },
      { id: "d", name: "OneDriveStandaloneUpdater", command: "%localappdata%\\Microsoft\\OneDrive\\OneDriveStandaloneUpdater.exe", source: "Планировщик", kind: "task", enabled: true }
    ],
    "startup.toggle": () => ({ ok: true }),
    "startup.delete": () => ({ ok: true }),
    "services.list": () => [
      { name: "DiagTrack", display: "Функциональные возможности для подключенных пользователей и телеметрия", desc: "Служба отправки диагностических данных в Microsoft", start: "auto", status: "running", recommended: true, touched: false },
      { name: "SysMain", display: "SysMain", desc: "Поддерживает и улучшает производительность системы", start: "auto", status: "running", recommended: false, touched: false },
      { name: "Spooler", display: "Диспетчер печати", desc: "Загружает файлы в память для последующей печати", start: "auto", status: "running", recommended: false, touched: false },
      { name: "WSearch", display: "Windows Search", desc: "Индексирование контента и кэширование свойств", start: "delayed", status: "running", recommended: false, touched: false },
      { name: "Fax", display: "Факс", desc: "Позволяет отправлять и получать факсы", start: "disabled", status: "stopped", recommended: true, touched: true }
    ],
    "services.set": (p) => ({ ok: true, start: p.mode, status: p.mode === "disabled" ? "stopped" : "running" }),
    "services.control": (p) => ({ ok: true, status: p.action === "start" ? "running" : "stopped" }),
    "appx.list": () => [
      { name: "Microsoft.XboxGamingOverlay", display: "Xbox Game Bar", group: "xbox", framework: false, system: true },
      { name: "Microsoft.GamingApp", display: "Xbox", group: "xbox", framework: false, system: true },
      { name: "Microsoft.BingNews", display: "Новости", group: "bing", framework: false, system: false },
      { name: "Microsoft.BingWeather", display: "Погода", group: "bing", framework: false, system: false },
      { name: "Microsoft.Copilot", display: "Copilot", group: "ai", framework: false, system: false },
      { name: "MSTeams", display: "Microsoft Teams", group: "social", framework: false, system: false },
      { name: "Clipchamp.Clipchamp", display: "Clipchamp", group: "media", framework: false, system: false },
      { name: "Microsoft.WindowsStore", display: "Microsoft Store", group: "essential", framework: false, system: true }
    ],
    "appx.remove": (p) => p.names.map((n) => ({ name: n, result: "ok" })),
    "net.adapters": () => ({
      adapters: [
        { id: "1", name: "Ethernet", desc: "Realtek Gaming 2.5GbE Family Controller", up: true, speed: 2500, ip: "192.168.1.42", dns: ["192.168.1.1"], nagle: false },
        { id: "2", name: "Wi-Fi", desc: "Intel(R) Wi-Fi 6E AX211", up: false, speed: 0, ip: "", dns: [], nagle: false }
      ],
      tcp: { autotuning: "normal", ecn: "disabled", rss: "enabled", rsc: "enabled", timestamps: "disabled", raw: "" },
      telemetry: false,
      hosts: 0
    }),
    "net.dns": () => [
      { id: "cloudflare", name: "Cloudflare", primary: "1.1.1.1", secondary: "1.0.0.1", noteRu: "Быстрый и без логов", noteEn: "", ping: -1 },
      { id: "google", name: "Google", primary: "8.8.8.8", secondary: "8.8.4.4", noteRu: "Стабильный, есть везде", noteEn: "", ping: -1 },
      { id: "adguard", name: "AdGuard", primary: "94.140.14.14", secondary: "94.140.15.15", noteRu: "Режет рекламу на уровне DNS", noteEn: "", ping: -1 }
    ],
    "net.testDns": () => handlers["net.dns"]().map((d, i) => Object.assign({}, d, { ping: 12 + i * 7 })),
    "net.ping": () => [
      { name: "Cloudflare", host: "1.1.1.1", ping: 11, loss: 0 },
      { name: "Google", host: "8.8.8.8", ping: 18, loss: 0 },
      { name: "Steam", host: "steamcommunity.com", ping: 34, loss: 0 },
      { name: "Klondaik", host: "klondaik.uk", ping: 46, loss: 0 }
    ],
    "net.telemetry": (p) => ({ blocked: p.block, count: p.block ? 30 : 0 }),
    "net.nagle": () => ({ ok: true }),
    "bench.history": () => [
      { utc: "2026-09-18T15:02:00Z", label: "После твиков", cpuSingle: 1842, cpuMulti: 21840, ramGbs: 18.42, diskWrite: 4821, diskRead: 6120, latencyAvg: 1.12, latencyMax: 3.4, bootSeconds: 14.2, score: 4820 },
      { utc: "2026-09-18T14:10:00Z", label: "До твиков", cpuSingle: 1790, cpuMulti: 21210, ramGbs: 18.1, diskWrite: 4710, diskRead: 6010, latencyAvg: 1.94, latencyMax: 8.1, bootSeconds: 22.8, score: 4410 }
    ],
    "bench.run": () => handlers["bench.history"]()[0],
    "bench.clear": () => ({ ok: true }),
    "soft.list": () => ({
      winget: true,
      items: software.items.map((s, i) => Object.assign({}, s, { installed: i % 5 === 0 }))
    }),
    "soft.install": () => ({ result: "ok" }),
    "soft.uninstall": () => ({ result: "ok" }),
    "journal.list": () => [
      { id: "1", tweakId: "priv.telemetry-off", title: "Отключить телеметрию", group: "tweak", time: "2026-09-18 19:04", reverted: false, items: 4, risk: "safe" },
      { id: "2", tweakId: "game.gamedvr-off", title: "Отключить Game DVR", group: "tweak", time: "2026-09-18 19:04", reverted: false, items: 4, risk: "safe" },
      { id: "3", tweakId: "sec.vbs-off", title: "Отключить виртуализацию безопасности (VBS)", group: "tweak", time: "2026-09-18 18:52", reverted: true, items: 4, risk: "extreme" }
    ],
    "journal.revert": () => ({ ok: true }),
    "journal.revertAll": () => ({ reverted: 3, failed: 0 }),
    "journal.clear": () => ({ ok: true }),
    "monitor.trim": () => ({ processes: 214, freed: 2847362819 }),
    "boost.start": () => ({ active: true, services: { SysMain: "running", WSearch: "running" }, freedRam: 1847362819 }),
    "boost.stop": () => ({ active: false, services: {}, freedRam: 0 }),
    "app.credits": () => creditsDb,
    "repair.run": (p) => ({ ok: true, changed: 12, skipped: 3, message: "возвращено служб: 12", details: [] }),
    "repair.status": () => ({ ctxti: true, ctxown: false }),
    "irq.list": () => ({
      hybrid: true,
      threads: Array.from({ length: 28 }, (_, i) => (i < 16
        ? { index: i, core: Math.floor(i / 2), efficiency: 1, performance: true }
        : { index: i, core: 8 + (i - 16), efficiency: 0, performance: false })),
      devices: [
        { id: 'PCI-GPU', name: 'NVIDIA GeForce RTX 5060 Ti', kind: 'gpu', service: 'nvlddmkm', policy: 4, maskHex: '0x4', threads: [2], priority: 3, bound: true },
        { id: 'PCI-USB', name: 'Intel(R) USB 3.20 xHCI', kind: 'usb', service: 'USBXHCI', policy: 4, maskHex: '0x10', threads: [4], priority: 0, bound: true },
        { id: 'PCI-NET', name: 'Realtek Gaming 2.5GbE Family Controller', kind: 'net', service: 'rt25cx21', policy: 0, maskHex: '', threads: [], priority: 3, bound: false },
        { id: 'PCI-NVME', name: 'Standard NVM Express Controller', kind: 'storage', service: 'stornvme', policy: 5, maskHex: '', threads: [], priority: 3, bound: false }
      ]
    }),
    "irq.bind": () => ({ ok: true, changed: 1, message: 'прерывания привязаны, изменение вступит в силу после перезагрузки', details: [] }),
    "irq.reset": () => ({ ok: true, changed: 1, message: 'привязка снята, изменение вступит в силу после перезагрузки', details: [] }),
    "tasks.list": () => [
      { id: "telemetry", title: "Сбор данных о работе системы", desc: "Задачи программы улучшения качества: раз в сутки собирают, какими программами вы пользовались, и отправляют в Microsoft.", rec: "off", enabled: 2, missing: 1,
        tasks: [
          { path: "\\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator", name: "Consolidator", enabled: true },
          { path: "\\Microsoft\\Windows\\Application Experience\\Microsoft Compatibility Appraiser", name: "Microsoft Compatibility Appraiser", enabled: true },
          { path: "\\Microsoft\\Windows\\Autochk\\Proxy", name: "Proxy", enabled: false }
        ] },
      { id: "maps", title: "Карты и местоположение", desc: "Фоновое обновление офлайн-карт — сотни мегабайт по расписанию.", rec: "off", enabled: 1, missing: 0,
        tasks: [
          { path: "\\Microsoft\\Windows\\Maps\\MapsUpdateTask", name: "MapsUpdateTask", enabled: true },
          { path: "\\Microsoft\\Windows\\Maps\\MapsToastTask", name: "MapsToastTask", enabled: false }
        ] },
      { id: "cleanup", title: "Автоматическая уборка", desc: "Плановая очистка диска, дефрагментация и обслуживание.", rec: "keep", enabled: 2, missing: 0,
        tasks: [
          { path: "\\Microsoft\\Windows\\DiskCleanup\\SilentCleanup", name: "SilentCleanup", enabled: true },
          { path: "\\Microsoft\\Windows\\Defrag\\ScheduledDefrag", name: "ScheduledDefrag", enabled: true }
        ] }
    ],
    "tasks.setGroup": () => ({ ok: true, changed: 2, message: "отключено задач: 2", details: [] }),
    "tasks.setOne": () => ({ ok: true, changed: 1, message: "задача отключена", details: [] }),
    "features.list": () => [
      { name: "MicrosoftWindowsPowerShellV2Root", title: "PowerShell 2.0", desc: "Версия 2007 года, оставленная для совместимости. Её любят вредоносные скрипты именно потому, что она не умеет логировать свои действия.", rec: "insecure", group: "legacy", state: "enabled", known: true },
      { name: "SMB1Protocol", title: "Протокол SMB 1.0", desc: "Старый протокол общих папок, через который распространялись WannaCry и NotPetya.", rec: "insecure", group: "legacy", state: "disabled", known: true },
      { name: "LegacyComponents", title: "Компоненты прежних версий", desc: "DirectPlay — сетевая подсистема игр девяностых.", rec: "off", group: "legacy", state: "disabled", known: true },
      { name: "Printing-XPSServices-Features", title: "Средство записи XPS-документов", desc: "Виртуальный принтер в формат XPS, которым никто не пользуется.", rec: "off", group: "print", state: "enabled", known: true },
      { name: "Printing-PrintToPDFServices-Features", title: "Печать в PDF (Майкрософт)", desc: "Виртуальный принтер, который сохраняет документ в PDF.", rec: "keep", group: "print", state: "enabled", known: true },
      { name: "MediaPlayback", title: "Компоненты для работы с мультимедиа", desc: "Базовые кодеки и проигрыватель.", rec: "risky", group: "media", state: "enabled", known: true },
      { name: "WorkFolders-Client", title: "Рабочие папки", desc: "Корпоративная синхронизация файлов с сервером организации.", rec: "off", group: "corp", state: "enabled", known: true },
      { name: "IIS-WebServerRole", title: "Службы IIS", desc: "Полноценный веб-сервер Microsoft.", rec: "off", group: "server", state: "disabled", known: true },
      { name: "VirtualMachinePlatform", title: "Платформа виртуальной машины", desc: "Нужна для WSL 2 и Android-подсистемы.", rec: "keep", group: "virt", state: "enabled", known: true },
      { name: "NetFx4-AdvSrvs", title: ".NET Framework 4.8 расширенные службы", desc: "Базовая часть современного .NET.", rec: "keep", group: "runtime", state: "enabled", known: true }
    ],
    "features.set": () => ({ ok: true, changed: 1, message: "компонент отключён", details: [] }),
    "sys.link": () => ({ ok: true }),
    "sys.folder": () => ({ ok: true }),
    "restore.open": () => ({ ok: true }),
    "sys.power": () => ({ ok: true }),
    "app.quit": () => ({ ok: true }),
    "update.check": () => ({
      available: true,
      current: "1.0.0",
      latest: "v1.1.0",
      size: 56623104,
      url: "https://github.com/Faliseven/klondaik-tweaker/releases/download/v1.1.0/KlondaikTweaker.exe",
      page: "https://github.com/Faliseven/klondaik-tweaker/releases",
      notes: "Добавлена категория «Слабый ПК» — 9 твиков\nПочинена запись значений больше 0x7FFFFFFF\nТочка восстановления создаётся быстрее"
    }),
    "update.install": () => ({ ok: true, restarting: true })
  };

  window.chrome = window.chrome || {};
  window.chrome.webview = {
    addEventListener(type, fn) {
      if (type === "message") listeners.add(fn);
    },
    async postMessage(raw) {
      const msg = JSON.parse(raw);
      if (!msg.id) return;
      if (!tweaks) {
        tweaks = await data("tweaks");
        wizard = await data("wizard");
        software = await data("software");
        try { creditsDb = await data("credits"); } catch (e) {}
      }
      const fn = handlers[msg.method];
      let payload;
      if (!fn) {
        payload = { id: msg.id, ok: false, error: "mock: " + msg.method };
      } else {
        payload = { id: msg.id, ok: true, result: fn(msg.payload || {}) };
      }
      setTimeout(() => listeners.forEach((l) => l({ data: JSON.stringify(payload) })), 120);
    }
  };

  const query = new URLSearchParams(location.search);
  if (query.get("accept") === "1") settings.acceptedRisk = true;
  if (query.get("still") === "1") settings.reduced = true;
  if (query.get("lang")) settings.lang = query.get("lang");
  const startPage = query.get("p");
  if (startPage) {
    const tryGo = (left) => {
      const btn = document.querySelector('.navitem[data-page="' + startPage + '"]');
      if (btn) btn.click();
      else if (left > 0) setTimeout(() => tryGo(left - 1), 200);
    };
    window.addEventListener("load", () => setTimeout(() => tryGo(30), 400));
  }

  const clicks = query.getAll("click");
  if (clicks.length) {
    const run = (i) => {
      if (i >= clicks.length) return;
      const el = document.querySelector(clicks[i]);
      if (el) el.click();
      setTimeout(() => run(i + 1), 600);
    };
    window.addEventListener("load", () => setTimeout(() => run(0), 1600));
  }

  setInterval(() => {
    const m = {
      cpu: +(8 + Math.random() * 22).toFixed(1),
      ram: 41.6,
      ramUsed: 28472910283,
      ramTotal: 68481302528,
      gpu: +(4 + Math.random() * 30).toFixed(1),
      gpuMem: 2841,
      disk: +(Math.random() * 18).toFixed(1),
      netDown: +(Math.random() * 900).toFixed(1),
      netUp: +(Math.random() * 120).toFixed(1),
      processes: 241,
      cpuTemp: 46.5,
      gpuTemp: 51,
      uptime: 92840
    };
    listeners.forEach((l) => l({ data: JSON.stringify({ evt: "metrics", data: m }) }));
  }, 1500);
})();
