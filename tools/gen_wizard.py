import json
import os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "KlondaikTweaker", "Assets", "Data", "wizard.json")


def q(qid, ru, en, options, multi=False, sru=None, sen=None, when=None):
    item = {"id": qid, "ru": ru, "en": en, "multi": multi, "options": options}
    if sru:
        item["sru"] = sru
    if sen:
        item["sen"] = sen
    if when:
        item["when"] = when
    return item


def o(oid, ru, en, hru=None, hen=None):
    item = {"id": oid, "ru": ru, "en": en}
    if hru:
        item["hru"] = hru
    if hen:
        item["hen"] = hen
    return item


def rule(when, add=(), drop=()):
    return {"when": when, "add": list(add), "drop": list(drop)}


QUESTIONS = [
    q("use", "Для чего вы используете этот компьютер?", "What do you use this PC for?",
      [o("gaming", "Игры", "Gaming", "Основная нагрузка — игры", "Games are the main load"),
       o("work", "Работа и учёба", "Work and study", "Документы, браузер, видеозвонки", "Documents, browser, calls"),
       o("media", "Видео и музыка", "Video and music", "Просмотр, монтаж, стриминг", "Watching, editing, streaming"),
       o("dev", "Программирование", "Development", "Компиляция, виртуалки, контейнеры", "Compiling, VMs, containers"),
       o("browse", "Интернет и общение", "Browsing and chat", "Соцсети, мессенджеры, сайты", "Social, messengers, sites")],
      multi=True, sru="Можно выбрать несколько", sen="Pick as many as apply"),

    q("level", "Насколько вы уверенно чувствуете себя в настройках Windows?", "How comfortable are you with Windows internals?",
      [o("novice", "Новичок", "Beginner", "Хочу, чтобы стало быстрее, и ничего не сломалось", "I want it faster without breaking anything"),
       o("confident", "Уверенный пользователь", "Confident user", "Знаю, что такое реестр и службы", "I know what the registry and services are"),
       o("pro", "Опытный", "Advanced", "Понимаю последствия и умею откатывать", "I understand the trade-offs and can roll back")],
      sru="От этого зависит, какие твики вам вообще предложат", sen="This decides which tweaks are offered at all"),

    q("risk", "Насколько глубоко резать систему?", "How deep should we cut?",
      [o("safe", "Только безопасное", "Safe only", "Ничего, что отключает защиту или обновления", "Nothing that disables protection or updates"),
       o("medium", "Средний риск", "Moderate", "Можно трогать службы и планировщик", "Services and the scheduler are fair game"),
       o("max", "Максимум", "Maximum", "Включая Spectre-патчи и виртуализацию безопасности", "Including Spectre patches and VBS")],
      sru="Любой твик можно откатить по одному в журнале", sen="Every tweak can be reverted individually in the journal"),

    q("games", "Какие игры вы запускаете чаще всего?", "Which games do you play most?",
      [o("competitive", "Соревновательные шутеры", "Competitive shooters", "CS2, Valorant, Apex — важна задержка", "CS2, Valorant, Apex — latency matters"),
       o("aaa", "Крупные одиночные", "Big single-player", "Важен FPS и стабильность кадров", "FPS and frame stability matter"),
       o("casual", "Нетребовательные и онлайн", "Casual and online", "Dota, WoT, браузерные", "Dota, WoT, browser games")],
      when="use=gaming"),

    q("anticheat", "Играете в игры с античитом уровня ядра?", "Do you play games with kernel-level anti-cheat?",
      [o("yes", "Да", "Yes", "Valorant, Faceit, ESEA, LoL", "Valorant, Faceit, ESEA, LoL"),
       o("no", "Нет", "No", "Обычные игры из Steam", "Regular Steam games")],
      when="use=gaming",
      sru="Vanguard и Faceit требуют включённой изоляции ядра — при ответе «да» мы её не тронем",
      sen="Vanguard and Faceit require core isolation, so we will leave it alone if you say yes"),

    q("privacy", "Что делаем с телеметрией и сбором данных?", "What about telemetry and data collection?",
      [o("max", "Резать всё", "Cut everything", "Телеметрия, геолокация, реклама, Copilot, Recall", "Telemetry, location, ads, Copilot, Recall"),
       o("balanced", "Разумный баланс", "Reasonable balance", "Убрать сбор данных, оставить удобства", "Remove data collection, keep conveniences"),
       o("none", "Не трогать", "Leave it", "Меня устраивает как есть", "I am fine with the defaults")]),

    q("defender", "Как быть с Защитником Windows?", "What about Windows Defender?",
      [o("keep", "Оставить как есть", "Keep it", "Самый безопасный вариант", "The safe answer"),
       o("other", "У меня другой антивирус", "I use another antivirus", "Защитник и так отключится сам", "Defender turns itself off anyway"),
       o("off", "Отключить полностью", "Disable completely", "Понимаю риск, нужна производительность", "I accept the risk, I want the performance")]),

    q("updates", "Как вы хотите получать обновления Windows?", "How do you want Windows updates?",
      [o("auto", "Автоматически", "Automatically", "Только запретить перезагрузку без спроса", "Just block unattended reboots"),
       o("manual", "Вручную", "Manually", "Скачивать и ставить по кнопке", "Download and install on demand"),
       o("off", "Полностью отключить", "Fully disable", "Понимаю, что не будет и патчей безопасности", "I understand security patches stop too")]),

    q("printer", "Пользуетесь принтером?", "Do you use a printer?",
      [o("yes", "Да", "Yes"), o("no", "Нет", "No", "Отключим диспетчер печати", "We will disable the print spooler")]),

    q("bluetooth", "Пользуетесь Bluetooth?", "Do you use Bluetooth?",
      [o("yes", "Да", "Yes", "Наушники, мышь, геймпад", "Headphones, mouse, gamepad"),
       o("no", "Нет", "No", "Отключим три службы", "We will disable three services")]),

    q("xbox", "Играете в игры из Game Pass или через Xbox?", "Do you use Game Pass or Xbox?",
      [o("yes", "Да", "Yes"), o("no", "Нет", "No", "Уберём службы и приложения Xbox", "We will remove Xbox services and apps")]),

    q("hello", "Как вы входите в Windows?", "How do you sign in to Windows?",
      [o("hello", "Отпечаток, лицо или PIN-устройство", "Fingerprint, face or security key"),
       o("password", "Пароль или обычный PIN", "Password or plain PIN", "Отключим биометрию и смарт-карты", "We will disable biometrics and smart cards")]),

    q("onedrive", "Пользуетесь OneDrive?", "Do you use OneDrive?",
      [o("yes", "Да", "Yes"), o("no", "Нет", "No", "Уберём его из проводника", "We will hide it in Explorer")]),

    q("search", "Ищете файлы через поиск Windows?", "Do you search files with Windows Search?",
      [o("yes", "Да, постоянно", "Yes, all the time"),
       o("no", "Нет, у меня Everything", "No, I use Everything", "Отключим индексацию — минус фоновая нагрузка на диск", "We will disable indexing — less background disk load")]),

    q("ui", "Что убрать из интерфейса?", "What should we clean up in the interface?",
      [o("taskbar", "Поиск, виджеты и чат с панели задач", "Search, widgets and chat from the taskbar"),
       o("left", "Вернуть кнопки влево", "Move taskbar buttons left"),
       o("classic", "Классическое меню правой кнопки", "Classic right-click menu"),
       o("start", "Блок «Рекомендуем» в Пуске", "The Recommended block in Start"),
       o("dark", "Включить тёмную тему", "Switch to dark theme"),
       o("anim", "Анимации окон", "Window animations")],
      multi=True, sru="Чисто вкусовые вещи, на производительность почти не влияют", sen="Taste, with barely any performance impact"),

    q("cleanup", "Удалять предустановленные приложения?", "Remove preinstalled apps?",
      [o("aggressive", "Да, всё лишнее", "Yes, everything unnecessary", "Bing, Teams, Skype, медиа, Copilot", "Bing, Teams, Skype, media, Copilot"),
       o("light", "Только очевидный мусор", "Only the obvious junk", "Bing и встроенные медиаприложения", "Bing and bundled media apps"),
       o("none", "Ничего не трогать", "Leave them alone")]),
]

BASE = [
    "perf.startup-delay", "perf.menu-delay", "perf.kill-timeouts", "perf.wer-off",
    "ui.file-extensions", "priv.ceip-tasks", "priv.appcompat-off", "priv.suggestions-off",
    "priv.tips-off", "priv.feedback-off", "priv.tailored-off", "priv.advertising-id",
    "priv.activity-history", "perf.ntfs-lastaccess", "perf.clear-pagefile-off",
]

EXTREME = [
    "sec.vbs-off", "sec.defender-off", "sec.smartscreen-off", "sec.uac-lower",
    "perf.mitigations-off", "perf.dynamic-tick-off", "perf.tsc-sync", "upd.full-off",
]

RULES = [
    rule("use=gaming", [
        "game.gamedvr-off", "game.gamebar-off", "game.gamemode-on", "perf.priority-separation",
        "perf.system-responsiveness", "perf.network-throttling", "perf.games-task",
        "perf.mouse-accel", "perf.keyboard-speed", "pwr.high-performance", "net.tcp-tuning",
        "net.qos-off", "net.dns-cache", "ui.sticky-keys-off", "perf.fast-startup-off",
    ]),
    rule("games=competitive", [
        "game.fso-off", "gpu.hags-on", "perf.paging-executive", "pwr.core-parking-off",
        "pwr.usb-suspend-off", "svc.netdata-off", "perf.power-throttling-off",
    ]),
    rule("games=aaa", ["gpu.hags-on", "gpu.tdr-delay", "perf.power-throttling-off"]),
    rule("use=dev", ["perf.svchost-split", "perf.paging-executive"], ["perf.8dot3-off"]),
    rule("use=media", ["pwr.disk-timeout-off"]),

    rule("privacy=max", [
        "priv.telemetry-off", "priv.diagtrack-off", "priv.typing-off", "priv.speech-off",
        "priv.sync-off", "priv.cloud-clipboard-off", "priv.edge-telemetry",
        "priv.delivery-optimization", "priv.location-off", "priv.hosts-telemetry",
        "priv.background-apps-off", "ai.bing-search-off", "ai.cortana-off", "ai.copilot-off",
        "ai.recall-off", "ui.recent-off", "disk.storage-sense-off", "net.llmnr-off",
        "net.wifi-sense-off",
    ]),
    rule("privacy=balanced", [
        "priv.telemetry-off", "priv.diagtrack-off", "priv.edge-telemetry",
        "priv.delivery-optimization", "priv.sync-off", "ai.bing-search-off",
        "ai.copilot-off", "ai.recall-off", "net.llmnr-off",
    ]),

    rule("defender=off", ["sec.defender-off", "sec.smartscreen-off"]),
    rule("defender=other", ["sec.smartscreen-off"]),

    rule("updates=auto", ["upd.no-auto-restart"]),
    rule("updates=manual", ["upd.manual", "upd.no-driver-updates", "upd.no-auto-restart", "upd.store-auto-off"]),
    rule("updates=off", ["upd.full-off", "upd.no-driver-updates"]),

    rule("printer=no", ["svc.print-off"]),
    rule("bluetooth=no", ["svc.bluetooth-off"]),
    rule("xbox=no", ["game.xbox-services-off", "app.debloat-xbox"]),
    rule("hello=password", ["svc.smartcard-off"]),
    rule("onedrive=no", ["ui.onedrive-hide"]),
    rule("search=no", ["perf.search-index-off"]),

    rule("ui=taskbar", ["ui.taskbar-clean"]),
    rule("ui=left", ["ui.taskbar-left"]),
    rule("ui=classic", ["ui.classic-context"]),
    rule("ui=start", ["ui.start-recommend-off"]),
    rule("ui=dark", ["ui.dark-mode"]),
    rule("ui=anim", ["ui.animations-off", "ui.transparency-off", "ui.snap-suggestions-off"]),

    rule("cleanup=aggressive", ["app.debloat-bing", "app.debloat-social", "app.debloat-media", "app.debloat-ai"]),
    rule("cleanup=light", ["app.debloat-bing", "app.debloat-media"]),

    rule("disk=ssd", ["perf.sysmain-off", "perf.prefetch-off", "disk.defrag-ssd-off", "disk.trim-on"]),
    rule("disk=hdd", ["pwr.disk-timeout-off"], ["perf.sysmain-off", "perf.prefetch-off"]),
    rule("gpu=nvidia", ["gpu.nvidia-telemetry"]),
    rule("device=desktop", ["pwr.ultimate", "svc.touch-off"]),
    rule("device=laptop", [], ["perf.power-throttling-off", "pwr.ultimate", "perf.dynamic-tick-off", "pwr.core-parking-off", "svc.touch-off"]),

    rule("risk=medium", ["svc.legacy-off", "svc.diagnostics-off", "svc.insider-off", "svc.remote-off", "perf.fast-startup-off", "perf.svchost-split"]),
    rule("risk=max", [
        "svc.legacy-off", "svc.diagnostics-off", "svc.insider-off", "svc.remote-off",
        "perf.fast-startup-off", "perf.svchost-split", "perf.mitigations-off",
        "sec.vbs-off", "perf.mem-compression-off", "perf.8dot3-off", "disk.reserved-storage-off",
    ]),
    rule("risk=safe", [], EXTREME + ["svc.print-off", "svc.remote-off", "svc.smartcard-off", "perf.mem-compression-off", "perf.8dot3-off"]),
    rule("level=novice", [], EXTREME),
    rule("anticheat=yes", [], ["sec.vbs-off", "perf.mitigations-off"]),
]

db = {"questions": QUESTIONS, "rules": RULES, "base": BASE}
os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, "w", encoding="utf-8") as f:
    json.dump(db, f, ensure_ascii=False, separators=(",", ":"))
print("questions: %d, rules: %d, base: %d" % (len(QUESTIONS), len(RULES), len(BASE)))
print("written: %s (%d bytes)" % (os.path.normpath(OUT), os.path.getsize(OUT)))
