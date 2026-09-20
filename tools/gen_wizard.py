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
    q("use", "Для чего вы чаще всего садитесь за этот компьютер?",
      "What do you mostly sit down at this computer to do?",
      [o("gaming", "Играть", "Gaming",
         "Игры — основная нагрузка", "Games are the main load"),
       o("work", "Работать или учиться", "Work or study",
         "Документы, почта, браузер, видеозвонки", "Documents, mail, browser, video calls"),
       o("media", "Смотреть видео, слушать музыку, монтировать", "Watch video, listen to music, edit",
         "Плееры, стриминг, обработка видео", "Players, streaming, video editing"),
       o("dev", "Программировать", "Write code",
         "Сборка проектов, виртуалки, контейнеры", "Building projects, VMs, containers"),
       o("browse", "Сидеть в интернете и общаться", "Browse and chat",
         "Соцсети, мессенджеры, сайты", "Social networks, messengers, sites")],
      multi=True,
      sru="Отметьте всё, что подходит — от этого зависит, что мы вам предложим",
      sen="Tick everything that applies — it decides what we suggest"),

    q("depth", "Насколько сильно вмешиваться в Windows?",
      "How far should we go inside Windows?",
      [o("safe", "Осторожно", "Carefully",
         "Только то, что точно ничего не сломает: реклама, слежка, мусор в интерфейсе. Защита и обновления останутся работать.",
         "Only what cannot break anything: ads, tracking, interface clutter. Protection and updates keep working."),
       o("medium", "Уверенно", "Confidently",
         "Плюс отключение ненужных служб и фоновых задач. Работает заметнее, но некоторые функции Windows пропадут.",
         "Plus turning off unneeded services and background tasks. A bigger gain, but some Windows features go away."),
       o("max", "По максимуму", "All the way",
         "Плюс отключение антивируса, защиты процессора и обновлений. Так быстрее всего, но компьютер станет уязвимее.",
         "Plus turning off the antivirus, CPU protections and updates. Fastest, but the machine becomes more exposed.")],
      sru="Любой твик потом откатывается по одному — в журнале видно каждое изменение",
      sen="Any tweak can be reverted one by one later — the journal lists every change"),

    q("age", "Как компьютер ведёт себя сейчас?",
      "How does the computer behave right now?",
      [o("slow", "Тормозит", "It is slow",
         "Долго включается, окна открываются с задержкой, всё думает",
         "Slow to boot, windows open with a delay, everything stalls"),
       o("ok", "Нормально, но хочется быстрее", "Fine, but I want it faster",
         "Работает, но иногда подтормаживает", "It works, but stutters now and then"),
       o("fast", "Быстро, настраиваю для запаса", "Fast, I am tuning for headroom",
         "Железо мощное, ищу последние проценты", "Strong hardware, chasing the last few percent")],
      sru="Если машина слабая, мы добавим то, что реально помогает старому железу",
      sen="On a weak machine we add the things that actually help old hardware"),

    q("games", "Во что играете чаще всего?",
      "What do you play most often?",
      [o("competitive", "Соревновательные шутеры", "Competitive shooters",
         "CS2, Valorant, Apex — важна не картинка, а задержка",
         "CS2, Valorant, Apex — latency matters more than looks"),
       o("aaa", "Большие одиночные игры", "Big single-player games",
         "Важны кадры в секунду и отсутствие рывков",
         "Frames per second and no stutter are what matter"),
       o("casual", "Нетребовательные и онлайн", "Undemanding and online",
         "Dota, World of Tanks, браузерные", "Dota, World of Tanks, browser games")],
      when="use=gaming"),

    q("anticheat", "Играете в игры, которые требуют включённой защиты Windows?",
      "Do you play games that require Windows protection to stay on?",
      [o("yes", "Да", "Yes",
         "Valorant, Faceit, ESEA, League of Legends. Их защита не запустится, если отключить изоляцию ядра.",
         "Valorant, Faceit, ESEA, League of Legends. Their protection refuses to start if core isolation is off."),
       o("no", "Нет", "No",
         "Обычные игры из Steam, Epic, GOG", "Regular games from Steam, Epic, GOG"),
       o("dunno", "Не знаю", "Not sure",
         "Тогда не будем трогать защиту — так безопаснее",
         "Then we leave the protection alone, which is the safe choice")],
      when="use=gaming",
      sru="Некоторые онлайн-игры проверяют, включена ли защита, и без неё не запускаются",
      sen="Some online games check whether protection is on and refuse to start without it"),

    q("xbox", "Пользуетесь Xbox Game Pass или приложением Xbox?",
      "Do you use Xbox Game Pass or the Xbox app?",
      [o("yes", "Да", "Yes", "Оставим всё как есть", "We leave it all alone"),
       o("no", "Нет", "No",
         "Уберём приложение Xbox и его фоновые службы — это несколько процессов в памяти",
         "We remove the Xbox app and its background services, a few processes worth of memory")],
      when="use=gaming"),

    q("privacy", "Что делать со сбором данных о вас?",
      "What should we do about data collection?",
      [o("max", "Отключить всё, что можно", "Turn off everything possible",
         "Телеметрия, геолокация, рекламный профиль, Copilot, Recall, распознавание речи и рукописного ввода",
         "Telemetry, location, the advertising profile, Copilot, Recall, speech and handwriting recognition"),
       o("balanced", "Убрать слежку, оставить удобства", "Stop the tracking, keep the conveniences",
         "Отключим отправку данных, но оставим поиск, подсказки и синхронизацию",
         "We stop the data from being sent but keep search, suggestions and sync"),
       o("none", "Ничего не трогать", "Leave it as it is",
         "Меня всё устраивает", "I am fine with it")]),

    q("notify", "Мешают ли всплывающие уведомления?",
      "Do popup notifications get in the way?",
      [o("keep", "Нет, пусть будут", "No, keep them", None, None),
       o("off", "Да, убрать совсем", "Yes, remove them",
         "Карточки в правом нижнем углу и звук к ним пропадут. Центр уведомлений останется — там они будут копиться.",
         "The cards in the bottom right corner and their sound go away. The notification centre stays and still collects them.")]),

    q("defender", "Как быть с Защитником Windows?",
      "What about Windows Defender?",
      [o("keep", "Оставить включённым", "Leave it on",
         "Самый безопасный вариант. Уберём только назойливые уведомления от него.",
         "The safe choice. We only silence its nagging notifications."),
       o("other", "У меня свой антивирус", "I have my own antivirus",
         "Защитник отключится сам, когда увидит другой антивирус. Уберём только SmartScreen.",
         "Defender turns itself off once it sees another antivirus. We only remove SmartScreen."),
       o("off", "Отключить полностью", "Turn it off completely",
         "Компьютер останется без антивируса. Делайте так, только если понимаете риск.",
         "The machine is left without an antivirus. Only do this if you understand the risk.")]),

    q("updates", "Как получать обновления Windows?",
      "How should Windows updates arrive?",
      [o("auto", "Автоматически, как сейчас", "Automatically, as now",
         "Запретим только перезагрузку без спроса", "We only stop the reboot that does not ask"),
       o("manual", "Вручную, когда я сам решу", "By hand, when I decide",
         "Обновления перестанут ставиться сами, кнопка «Проверить» продолжит работать",
         "Updates stop installing on their own, the Check button keeps working"),
       o("off", "Отключить совсем", "Turn them off entirely",
         "Перестанут приходить и заплатки безопасности. Придётся следить за этим самому.",
         "Security patches stop arriving too. You will have to keep track of that yourself.")]),

    q("battery", "Что важнее на батарее?",
      "What matters more on battery?",
      [o("life", "Время работы", "Battery life",
         "Не будем трогать энергосбережение", "We leave the power saving alone"),
       o("speed", "Скорость", "Speed",
         "Ноутбук будет быстрее, но разрядится заметно раньше",
         "The laptop gets faster but runs down noticeably sooner")],
      when="device=laptop",
      sru="Мы определили, что это ноутбук", sen="We detected this is a laptop"),

    q("vm", "Пользуетесь виртуальными машинами, WSL или Docker?",
      "Do you use virtual machines, WSL or Docker?",
      [o("yes", "Да", "Yes",
         "Не будем отключать виртуализацию — без неё они не запустятся",
         "We leave virtualisation on, they will not start without it"),
       o("no", "Нет", "No", None, None)],
      when="use=dev"),

    q("devtel", "Отключить отправку данных из инструментов разработчика?",
      "Turn off the data developer tools send?",
      [o("yes", "Да", "Yes",
         "dotnet, PowerShell, Azure CLI, Next.js и другие перестанут сообщать о каждой сборке",
         "dotnet, PowerShell, Azure CLI, Next.js and others stop reporting every build"),
       o("no", "Нет", "No", None, None)],
      when="use=dev"),

    q("printer", "Печатаете что-нибудь с этого компьютера?",
      "Do you print anything from this computer?",
      [o("yes", "Да", "Yes", None, None),
       o("no", "Нет, принтера нет", "No, there is no printer",
         "Отключим диспетчер печати — это одна постоянно запущенная служба",
         "We turn off the print spooler, one permanently running service")]),

    q("bluetooth", "Подключаете что-нибудь по Bluetooth?",
      "Do you connect anything over Bluetooth?",
      [o("yes", "Да", "Yes", "Наушники, мышь, клавиатура, геймпад, телефон",
         "Headphones, mouse, keyboard, gamepad, phone"),
       o("no", "Нет", "No", "Отключим три службы Bluetooth", "We turn off three Bluetooth services")]),

    q("hello", "Как вы входите в Windows?",
      "How do you sign in to Windows?",
      [o("hello", "По отпечатку пальца или лицу", "By fingerprint or face",
         "Windows Hello — камера или сканер отпечатка",
         "Windows Hello — a camera or a fingerprint reader"),
       o("password", "Паролем или PIN-кодом", "By password or PIN",
         "Отключим биометрию и поддержку смарт-карт — ими вы не пользуетесь",
         "We turn off biometrics and smart card support, which you do not use")]),

    q("onedrive", "Пользуетесь OneDrive?",
      "Do you use OneDrive?",
      [o("yes", "Да", "Yes", None, None),
       o("no", "Нет", "No", "Уберём его из бокового меню проводника",
         "We take it out of the Explorer sidebar")]),

    q("search", "Ищете файлы через поиск Windows?",
      "Do you find files with Windows search?",
      [o("yes", "Да, постоянно", "Yes, all the time", None, None),
       o("no", "Нет, почти никогда", "No, almost never",
         "Отключим индексацию — она постоянно читает диск в фоне. Поиск останется, но станет медленнее.",
         "We turn off indexing, which reads the disk in the background. Search stays but gets slower.")]),

    q("browser", "Бывало, что Windows сама возвращала свой браузер по умолчанию?",
      "Has Windows ever put its own browser back as the default?",
      [o("yes", "Да, бесит", "Yes, and it is maddening",
         "Отключим драйвер, который это делает", "We turn off the driver that does it"),
       o("no", "Нет или не замечал", "No, or I never noticed", None, None)],
      sru="В Windows 11 есть драйвер, который молча откатывает смену браузера и почты",
      sen="Windows 11 has a driver that silently reverts a change of browser or mail client"),

    q("ui", "Что поправить во внешнем виде?",
      "What should we change about the look?",
      [o("taskbar", "Убрать поиск, виджеты и чат с панели задач",
         "Remove search, widgets and chat from the taskbar"),
       o("left", "Вернуть кнопки панели задач влево", "Move the taskbar buttons back to the left"),
       o("classic", "Вернуть старое меню правой кнопки", "Bring back the old right-click menu"),
       o("start", "Убрать «Рекомендуем» из Пуска", "Remove Recommended from the Start menu"),
       o("dark", "Включить тёмную тему", "Switch to the dark theme"),
       o("anim", "Отключить анимации окон", "Turn off window animations"),
       o("lock", "Пропускать экран блокировки", "Skip the lock screen"),
       o("wallpaper", "Не портить качество обоев", "Stop degrading the wallpaper quality"),
       o("photo", "Вернуть старый просмотрщик фотографий", "Bring back the old photo viewer"),
       o("tray", "Показывать все значки в трее", "Show every tray icon")],
      multi=True,
      sru="Выберите что нравится. На скорость это почти не влияет, кроме анимаций.",
      sen="Pick what you like. None of it affects speed much, except the animations."),

    q("cleanup", "Удалить предустановленные приложения?",
      "Remove the preinstalled apps?",
      [o("aggressive", "Да, всё лишнее", "Yes, everything unnecessary",
         "Bing, Teams, Skype, встроенные медиаприложения, Copilot",
         "Bing, Teams, Skype, the bundled media apps, Copilot"),
       o("light", "Только очевидный мусор", "Only the obvious junk",
         "Bing и встроенные медиаприложения", "Bing and the bundled media apps"),
       o("none", "Ничего не трогать", "Leave them alone", None, None)],
      sru="Удалённое приложение можно поставить обратно из магазина",
      sen="A removed app can be installed again from the Store"),
]

BASE = [
    "perf.startup-delay", "perf.menu-delay", "perf.kill-timeouts", "perf.wer-off",
    "ui.file-extensions", "priv.ceip-tasks", "priv.appcompat-off", "priv.suggestions-off",
    "priv.tips-off", "priv.feedback-off", "priv.tailored-off", "priv.advertising-id",
    "priv.activity-history", "perf.ntfs-lastaccess", "perf.clear-pagefile-off",
]

EXTREME = [
    "sec.vbs-off", "sec.defender-off", "sec.smartscreen-off", "sec.uac-lower",
    "sec.firewall-off", "perf.mitigations-off", "perf.dynamic-tick-off", "perf.tsc-sync",
    "upd.full-off",
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
        "perf.hpet-off", "perf.timer-resolution",
    ]),
    rule("games=aaa", ["gpu.hags-on", "gpu.tdr-delay", "perf.power-throttling-off", "perf.hpet-off"]),
    rule("use=dev", ["perf.svchost-split", "perf.paging-executive"], ["perf.8dot3-off"]),
    rule("use=media", ["pwr.disk-timeout-off"]),

    rule("age=slow", [
        "weak.visual-effects", "weak.icons-only", "weak.peek-off", "weak.fth-off",
        "weak.defender-scan-limit", "weak.discovery-off", "weak.startup-delay-off",
        "ui.transparency-off", "ui.animations-off", "ui.snap-suggestions-off",
        "priv.background-apps-off", "perf.maintenance-off", "priv.edge-background-off",
        "app.debloat-bing", "app.debloat-media",
    ], ["gpu.hags-on"]),
    rule("age=fast", ["perf.paging-executive"]),

    rule("privacy=max", [
        "priv.telemetry-off", "priv.diagtrack-off", "priv.typing-off", "priv.speech-off",
        "priv.sync-off", "priv.cloud-clipboard-off", "priv.edge-telemetry",
        "priv.delivery-optimization", "priv.location-off", "priv.hosts-telemetry",
        "priv.background-apps-off", "ai.bing-search-off", "ai.cortana-off", "ai.copilot-off",
        "ai.recall-off", "ui.recent-off", "disk.storage-sense-off", "net.llmnr-off",
        "net.wifi-sense-off", "priv.license-telemetry", "ui.spotlight-off",
        "ui.news-interests-off", "priv.edge-background-off", "upd.maps-off",
    ]),
    rule("privacy=balanced", [
        "priv.telemetry-off", "priv.diagtrack-off", "priv.edge-telemetry",
        "priv.delivery-optimization", "priv.sync-off", "ai.bing-search-off",
        "ai.copilot-off", "ai.recall-off", "net.llmnr-off", "ui.spotlight-off",
    ]),

    rule("notify=off", ["ui.notifications-off"]),

    rule("defender=keep", ["sec.defender-notify-off"], ["sec.defender-off"]),
    rule("defender=off", ["sec.defender-off", "sec.smartscreen-off"]),
    rule("defender=other", ["sec.smartscreen-off", "sec.defender-notify-off"]),

    rule("updates=auto", ["upd.no-auto-restart"]),
    rule("updates=manual", ["upd.manual", "upd.no-driver-updates", "upd.no-auto-restart", "upd.store-auto-off"]),
    rule("updates=off", ["upd.full-off", "upd.no-driver-updates"]),

    rule("battery=speed", ["pwr.high-performance", "perf.power-throttling-off"]),
    rule("battery=life", [], ["pwr.high-performance", "pwr.ultimate", "perf.power-throttling-off", "pwr.core-parking-off", "perf.hibernate-off"]),

    rule("vm=yes", [], ["sec.vbs-off"]),
    rule("devtel=yes", [
        "priv.dotnet-telemetry", "priv.powershell-telemetry", "priv.devtools-telemetry",
        "priv.vs-telemetry",
    ]),

    rule("printer=no", ["svc.print-off"]),
    rule("bluetooth=no", ["svc.bluetooth-off"]),
    rule("xbox=no", ["game.xbox-services-off", "app.debloat-xbox"]),
    rule("hello=password", ["svc.smartcard-off"]),
    rule("onedrive=no", ["ui.onedrive-hide"]),
    rule("search=no", ["perf.search-index-off"]),
    rule("browser=yes", ["priv.ucpd-off"]),

    rule("ui=taskbar", ["ui.taskbar-clean", "ui.taskbar-win11"]),
    rule("ui=left", ["ui.taskbar-left"]),
    rule("ui=classic", ["ui.classic-context"]),
    rule("ui=start", ["ui.start-recommend-off"]),
    rule("ui=dark", ["ui.dark-mode"]),
    rule("ui=anim", ["ui.animations-off", "ui.transparency-off", "ui.snap-suggestions-off"]),
    rule("ui=lock", ["ui.lockscreen-off"]),
    rule("ui=wallpaper", ["ui.wallpaper-quality"]),
    rule("ui=photo", ["ui.photo-viewer"]),
    rule("ui=tray", ["ui.tray-all-icons"]),

    rule("cleanup=aggressive", ["app.debloat-bing", "app.debloat-social", "app.debloat-media", "app.debloat-ai", "app.teams-autoinstall-off"]),
    rule("cleanup=light", ["app.debloat-bing", "app.debloat-media"]),

    rule("disk=ssd", ["perf.sysmain-off", "perf.prefetch-off", "disk.defrag-ssd-off", "disk.trim-on"]),
    rule("disk=hdd", ["pwr.disk-timeout-off", "weak.icons-only"], ["perf.sysmain-off", "perf.prefetch-off"]),
    rule("gpu=nvidia", ["gpu.nvidia-telemetry"]),
    rule("device=desktop", ["pwr.ultimate", "svc.touch-off"]),
    rule("device=laptop", [], ["perf.dynamic-tick-off", "svc.touch-off"]),

    rule("tier=weak", [
        "weak.visual-effects", "weak.icons-only", "weak.peek-off", "weak.fth-off",
        "weak.defender-scan-limit", "weak.memory-compression-on", "weak.discovery-off",
        "weak.pagefile-managed", "weak.startup-delay-off",
        "ui.transparency-off", "ui.animations-off", "ui.snap-suggestions-off",
        "priv.background-apps-off", "priv.appcompat-off", "perf.svchost-split",
        "perf.maintenance-off", "perf.wer-off", "perf.kill-timeouts",
        "app.debloat-bing", "app.debloat-media", "app.debloat-social",
    ], [
        "perf.mem-compression-off", "perf.paging-executive", "perf.paging-combining-off",
        "perf.hibernate-off", "gpu.hags-on", "perf.dynamic-tick-off",
    ]),
    rule("tier=strong", ["perf.paging-executive", "perf.svchost-split"]),

    rule("depth=medium", [
        "svc.legacy-off", "svc.diagnostics-off", "svc.insider-off", "svc.remote-off",
        "perf.fast-startup-off", "perf.svchost-split", "priv.ucpd-off",
    ]),
    rule("depth=max", [
        "svc.legacy-off", "svc.diagnostics-off", "svc.insider-off", "svc.remote-off",
        "perf.fast-startup-off", "perf.svchost-split", "perf.mitigations-off",
        "sec.vbs-off", "perf.mem-compression-off", "perf.8dot3-off", "disk.reserved-storage-off",
        "priv.ucpd-off", "sec.autoencrypt-off",
    ]),
    rule("depth=safe", [], EXTREME + [
        "svc.print-off", "svc.remote-off", "svc.smartcard-off",
        "perf.mem-compression-off", "perf.8dot3-off", "priv.ucpd-off",
    ]),

    rule("anticheat=yes", [], ["sec.vbs-off", "perf.mitigations-off"]),
    rule("anticheat=dunno", [], ["sec.vbs-off", "perf.mitigations-off"]),
]

KNOWN_KEYS = {q["id"] for q in QUESTIONS} | {"device", "disk", "gpu", "tier"}

problems = []
seen = set()
for item in QUESTIONS:
    if item["id"] in seen:
        problems.append("duplicate question id: " + item["id"])
    seen.add(item["id"])
    if item.get("when"):
        for clause in item["when"].split("&"):
            key = clause.split("!=")[0].split("=")[0].strip()
            if key not in KNOWN_KEYS:
                problems.append("question %s asks about unknown key %s" % (item["id"], key))
    ids = [x["id"] for x in item["options"]]
    if len(ids) != len(set(ids)):
        problems.append("duplicate option in " + item["id"])
    for opt in item["options"]:
        if not opt["ru"] or not opt["en"]:
            problems.append("empty option label in " + item["id"])

for r in RULES:
    for clause in r["when"].split("&"):
        key = clause.split("!=")[0].split("=")[0].strip()
        value = clause.split("!=")[-1].split("=")[-1].strip()
        if key not in KNOWN_KEYS:
            problems.append("rule uses unknown key: " + r["when"])
            continue
        if key in seen:
            opts = {o["id"] for item in QUESTIONS if item["id"] == key for o in item["options"]}
            if value not in opts:
                problems.append("rule %s uses unknown option %s" % (r["when"], value))

if problems:
    for p in problems:
        print("PROBLEM " + p)
    raise SystemExit(1)

db = {"questions": QUESTIONS, "rules": RULES, "base": BASE}
os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, "w", encoding="utf-8") as f:
    json.dump(db, f, ensure_ascii=False, separators=(",", ":"))

conditional = sum(1 for x in QUESTIONS if x.get("when"))
print("questions: %d (%d always, %d conditional), rules: %d, base: %d"
      % (len(QUESTIONS), len(QUESTIONS) - conditional, conditional, len(RULES), len(BASE)))
print("written: %s (%d bytes)" % (os.path.normpath(OUT), os.path.getsize(OUT)))
