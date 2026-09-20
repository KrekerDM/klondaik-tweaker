import json
import os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "KlondaikTweaker", "Assets", "Data", "tasks.json")

ALLOWED_REC = {"off", "keep"}
MS = "\\Microsoft\\Windows\\"


def g(gid, ru, en, dru, den, rec, paths):
    return {"id": gid, "ru": ru, "en": en, "dru": dru, "den": den, "rec": rec,
            "paths": [MS + p for p in paths]}


GROUPS = [
    g("telemetry", "Сбор данных о работе системы", "System data collection",
      "Задачи программы улучшения качества: раз в сутки собирают, какими программами вы пользовались, и отправляют в Microsoft.",
      "Customer experience improvement tasks: once a day they collect which programs you used and send it to Microsoft.",
      "off",
      ["Customer Experience Improvement Program\\Consolidator",
       "Customer Experience Improvement Program\\UsbCeip",
       "Customer Experience Improvement Program\\KernelCeipTask",
       "Autochk\\Proxy",
       "Application Experience\\Microsoft Compatibility Appraiser",
       "Application Experience\\ProgramDataUpdater",
       "Application Experience\\PcaPatchDbTask",
       "Application Experience\\StartupAppTask"]),

    g("diagnostics", "Диагностика и отчёты об ошибках", "Diagnostics and error reports",
      "Собирают дампы, журналы сбоев и сведения о конфигурации. Полезны, только если вы пишете в поддержку Microsoft.",
      "They collect dumps, crash logs and configuration details. Useful only if you file reports with Microsoft support.",
      "off",
      ["Windows Error Reporting\\QueueReporting",
       "DiskDiagnostic\\Microsoft-Windows-DiskDiagnosticDataCollector",
       "DiskFootprint\\Diagnostics",
       "Diagnosis\\Scheduled",
       "FileHistory\\File History (maintenance mode)",
       "Feedback\\Siuf\\DmClient",
       "Feedback\\Siuf\\DmClientOnScenarioDownload"]),

    g("insider", "Программа предварительной оценки", "Windows Insider programme",
      "Задачи сборок Insider. На обычной системе не делают ничего, но остаются в планировщике.",
      "Insider build tasks. On an ordinary system they do nothing but stay in the scheduler.",
      "off",
      ["Flighting\\FeatureConfig\\ReconcileFeatures",
       "Flighting\\FeatureConfig\\UsageDataFlushing",
       "Flighting\\FeatureConfig\\UsageDataReporting",
       "Flighting\\OneSettings\\RefreshCache"]),

    g("maps", "Карты и местоположение", "Maps and location",
      "Фоновое обновление офлайн-карт — сотни мегабайт по расписанию, даже если приложением «Карты» вы не пользуетесь.",
      "Background offline map updates — hundreds of megabytes on a schedule, even if you never open the Maps app.",
      "off",
      ["Maps\\MapsUpdateTask",
       "Maps\\MapsToastTask",
       "Location\\Notifications",
       "Location\\WindowsActionDialog"]),

    g("store", "Магазин и приложения", "Store and apps",
      "Фоновая проверка и установка обновлений приложений из магазина.",
      "Background checks and installs of Store app updates.",
      "keep",
      ["WindowsUpdate\\Scheduled Start",
       "InstallService\\ScanForUpdates",
       "InstallService\\ScanForUpdatesAsUser",
       "InstallService\\SmartRetry"]),

    g("xbox", "Xbox", "Xbox",
      "Сохранение игр в облако и учёт времени в играх. Не нужны, если Game Pass и приложением Xbox вы не пользуетесь.",
      "Cloud game saves and playtime tracking. Not needed if you do not use Game Pass or the Xbox app.",
      "off",
      ["XblGameSave\\XblGameSaveTask",
       "XblGameSave\\XblGameSaveTaskLogon",
       "Application Experience\\MareBackup"]),

    g("cleanup", "Автоматическая уборка", "Automatic housekeeping",
      "Плановая очистка диска, дефрагментация и обслуживание. На SSD пользы мало, а диск будится по расписанию.",
      "Scheduled disk cleanup, defrag and maintenance. Little use on an SSD, and the drive is woken on a schedule.",
      "keep",
      ["DiskCleanup\\SilentCleanup",
       "Defrag\\ScheduledDefrag",
       "MemoryDiagnostic\\ProcessMemoryDiagnosticEvents",
       "MemoryDiagnostic\\RunFullMemoryDiagnostic"]),

    g("proxy", "Автоопределение прокси", "Proxy auto-detect",
      "Поиск настроек прокси в сети при каждом подключении. В домашней сети только добавляет задержку при входе.",
      "Looking for proxy settings on the network at every connection. On a home network it only adds a delay at sign-in.",
      "off",
      ["NetTrace\\GatherNetworkInfo",
       "Wininet\\CacheTask"]),

    g("lang", "Языки и ввод", "Languages and input",
      "Загрузка языковых пакетов и обслуживание рукописного ввода.",
      "Downloading language packs and servicing handwriting input.",
      "off",
      ["LanguageComponentsInstaller\\Installation",
       "LanguageComponentsInstaller\\ReconcileLanguageResources",
       "LanguageComponentsInstaller\\Uninstallation",
       "TextServicesFramework\\MsCtfMonitor",
       "InputMethod\\SyncInputMethod"]),

    g("remote", "Удалённый доступ", "Remote access",
      "Задачи удалённого помощника и удалённого рабочего стола.",
      "Remote Assistance and Remote Desktop tasks.",
      "off",
      ["RemoteAssistance\\RemoteAssistanceTask",
       "RemoteApp and Desktop Connections Update\\Domain Joined Machine PnP Handler",
       "WorkFolders\\Work Folders Logon Synchronization",
       "WorkFolders\\Work Folders Maintenance Work"]),

    g("sync", "Синхронизация с Microsoft", "Microsoft sync",
      "Синхронизация настроек и лицензий с учётной записью Microsoft.",
      "Syncing settings and licences with the Microsoft account.",
      "off",
      ["SettingSync\\BackgroundUploadTask",
       "SettingSync\\BackupTask",
       "SettingSync\\NetworkStateChangeTask",
       "License Manager\\TempSignedLicenseExchange",
       "Subscription\\LicenseAcquisition"]),

    g("power", "Питание и режим сна", "Power and sleep",
      "Задачи, которые будят компьютер по расписанию для обслуживания.",
      "Tasks that wake the computer on a schedule for maintenance.",
      "keep",
      ["Power Efficiency Diagnostics\\AnalyzeSystem",
       "SystemRestore\\SR",
       "Sysmain\\ResPriStaticDbSync",
       "Sysmain\\WsSwapAssessmentTask"]),
]

problems = []
seen_group = set()
seen_path = {}
for item in GROUPS:
    if item["id"] in seen_group:
        problems.append("duplicate group: " + item["id"])
    seen_group.add(item["id"])
    if item["rec"] not in ALLOWED_REC:
        problems.append("%s: bad rec %s" % (item["id"], item["rec"]))
    if not item["paths"]:
        problems.append("%s: no tasks" % item["id"])
    for path in item["paths"]:
        if path in seen_path:
            problems.append("task in two groups: %s (%s, %s)" % (path, seen_path[path], item["id"]))
        seen_path[path] = item["id"]
        if "\\\\" in path or not path.startswith(MS):
            problems.append("%s: bad path %s" % (item["id"], path))
    for key in ("ru", "en", "dru", "den"):
        if not item[key].strip():
            problems.append("%s: empty %s" % (item["id"], key))

if problems:
    for p in problems:
        print("PROBLEM " + p)
    raise SystemExit(1)

db = {"version": 1, "groups": GROUPS}
os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, "w", encoding="utf-8") as out:
    json.dump(db, out, ensure_ascii=False, separators=(",", ":"))

print("groups: %d, tasks: %d" % (len(GROUPS), len(seen_path)))
print("written: %s (%d bytes)" % (os.path.normpath(OUT), os.path.getsize(OUT)))
