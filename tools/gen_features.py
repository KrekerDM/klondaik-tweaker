import json
import os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "KlondaikTweaker", "Assets", "Data", "features.json")

ALLOWED_REC = {"off", "keep", "risky", "insecure"}


def f(name, ru, en, dru, den, rec="keep", group="other"):
    return {
        "name": name, "ru": ru, "en": en, "dru": dru, "den": den,
        "rec": rec, "group": group,
    }


ITEMS = [
    f("NetFx3", ".NET Framework 3.5", ".NET Framework 3.5",
      "Нужен старым программам, написанным до 2012 года. Если ничего такого не запускаете, можно выключить — компонент скачивается обратно из интернета за пару минут.",
      "Needed by old programs written before 2012. If you run nothing like that it can go off — the component downloads back from the internet in a couple of minutes.",
      "keep", "runtime"),

    f("NetFx4-AdvSrvs", ".NET Framework 4.8 расширенные службы", ".NET Framework 4.8 Advanced Services",
      "Базовая часть современного .NET. Её требует половина установленных программ, включая эту.",
      "The core of modern .NET. Half the installed programs need it, this one included.",
      "keep", "runtime"),

    f("MicrosoftWindowsPowerShellV2Root", "PowerShell 2.0", "PowerShell 2.0",
      "Версия 2007 года, оставленная для совместимости. Её любят вредоносные скрипты именно потому, что она не умеет логировать свои действия. В системе есть PowerShell 5.1 и он полностью её заменяет.",
      "The 2007 version kept for compatibility. Malicious scripts favour it precisely because it cannot log what it does. PowerShell 5.1 is present and fully replaces it.",
      "insecure", "legacy"),

    f("SMB1Protocol", "Протокол SMB 1.0", "SMB 1.0 protocol",
      "Старый протокол общих папок, через который распространялись WannaCry и NotPetya. Microsoft убрала его из установки по умолчанию. Нужен только для доступа к очень старым сетевым хранилищам и принт-серверам.",
      "The old file sharing protocol WannaCry and NotPetya spread through. Microsoft removed it from the default install. Only needed to reach very old network storage and print servers.",
      "insecure", "legacy"),

    f("LegacyComponents", "Компоненты прежних версий", "Legacy components",
      "DirectPlay — сетевая подсистема игр девяностых и начала двухтысячных. Современные игры её не используют.",
      "DirectPlay, the networking layer of games from the nineties and early 2000s. Modern games do not use it.",
      "off", "legacy"),

    f("DirectPlay", "DirectPlay", "DirectPlay",
      "Часть компонентов прежних версий. Отдельно нужна только очень старым играм.",
      "Part of the legacy components. Only very old games need it on its own.",
      "off", "legacy"),

    f("WindowsMediaPlayer", "Проигрыватель Windows Media", "Windows Media Player",
      "Классический проигрыватель. Если музыку и видео смотрите в чём-то другом, компонент только занимает место и обновляется.",
      "The classic player. If you watch and listen in something else it just takes space and gets updated.",
      "off", "media"),

    f("MediaPlayback", "Компоненты для работы с мультимедиа", "Media features",
      "Базовые кодеки и проигрыватель. Отключение уносит с собой Windows Media Player и часть кодеков — браузеры и современные плееры свои кодеки носят с собой, но некоторые старые программы сломаются.",
      "The base codecs and the player. Turning it off takes Windows Media Player and some codecs with it — browsers and modern players carry their own, but some older programs will break.",
      "risky", "media"),

    f("Printing-Foundation-Features", "Службы печати и документов", "Printing and document services",
      "Основа печати. Если принтера нет и не будет, компонент можно снять целиком.",
      "The printing foundation. With no printer now or later the whole component can go.",
      "keep", "print"),

    f("Printing-PrintToPDFServices-Features", "Печать в PDF (Майкрософт)", "Microsoft Print to PDF",
      "Виртуальный принтер, который сохраняет документ в PDF. Удобная вещь, но если вы пользуетесь другим конвертером — лишний принтер в списке.",
      "A virtual printer that saves a document as PDF. Handy, but an extra printer in the list if you use a different converter.",
      "keep", "print"),

    f("Printing-XPSServices-Features", "Средство записи XPS-документов", "Microsoft XPS Document Writer",
      "Виртуальный принтер в формат XPS — попытка Microsoft сделать свой PDF, которой никто не пользуется.",
      "A virtual printer to the XPS format, Microsoft's attempt at its own PDF that nobody uses.",
      "off", "print"),

    f("Printing-Foundation-InternetPrinting-Client", "Печать через интернет", "Internet printing client",
      "Печать на принтер по протоколу IPP через интернет. В домашней сети не нужна.",
      "Printing to a printer over IPP across the internet. Not needed on a home network.",
      "off", "print"),

    f("WorkFolders-Client", "Рабочие папки", "Work Folders",
      "Корпоративная синхронизация файлов с сервером организации. Вне рабочего домена бесполезна.",
      "Corporate file sync with an organisation server. Useless outside a work domain.",
      "off", "corp"),

    f("ServicesForNFS-ClientOnly", "Клиент NFS", "NFS client",
      "Доступ к сетевым папкам Unix и NAS по протоколу NFS. Дома почти всегда используется SMB, а не NFS.",
      "Access to Unix and NAS shares over NFS. At home SMB is almost always used instead.",
      "off", "corp"),

    f("SmbDirect", "SMB Direct", "SMB Direct",
      "Ускорение общих папок на серверных сетевых картах с RDMA. На обычной домашней карте не делает ничего.",
      "Faster file shares on server network cards with RDMA. Does nothing on an ordinary home card.",
      "off", "corp"),

    f("IIS-WebServerRole", "Службы IIS", "IIS web server",
      "Полноценный веб-сервер Microsoft. Если вы не разрабатываете под ASP.NET и не держите сайт локально — это работающая служба и открытый порт без дела.",
      "Microsoft's full web server. Unless you develop for ASP.NET or host a site locally this is a running service and an open port doing nothing.",
      "off", "server"),

    f("IIS-HostableWebCore", "Внедряемое веб-ядро IIS", "IIS hostable web core",
      "Урезанный IIS, который программы могут поднимать внутри себя.",
      "A trimmed IIS that programs can host inside themselves.",
      "off", "server"),

    f("TelnetClient", "Клиент Telnet", "Telnet client",
      "Консольный клиент для протокола без шифрования. Пароли в telnet идут по сети открытым текстом.",
      "A console client for an unencrypted protocol. Telnet passwords travel the network in clear text.",
      "off", "legacy"),

    f("TFTP", "Клиент TFTP", "TFTP client",
      "Упрощённая передача файлов без пароля, используется для прошивки сетевого оборудования.",
      "Simplified file transfer with no password, used to flash network equipment.",
      "off", "legacy"),

    f("Windows-Identity-Foundation", "Windows Identity Foundation 3.5", "Windows Identity Foundation 3.5",
      "Старая библиотека корпоративной аутентификации для приложений на .NET 3.5.",
      "An old corporate authentication library for .NET 3.5 applications.",
      "off", "corp"),

    f("Containers", "Сервер контейнера", "Container server",
      "Поддержка контейнеров Windows. Нужна, если пользуетесь Docker с контейнерами Windows.",
      "Support for Windows containers. Needed if you use Docker with Windows containers.",
      "keep", "virt"),

    f("Containers-DisposableClientVM", "Песочница Windows", "Windows Sandbox",
      "Одноразовая виртуальная машина в один клик — удобно, чтобы запустить подозрительный файл. Место занимает, только когда запущена.",
      "A throwaway virtual machine in one click, handy for running a suspicious file. It only takes resources while running.",
      "keep", "virt"),

    f("Microsoft-Hyper-V-All", "Hyper-V", "Hyper-V",
      "Гипервизор Microsoft. Включённый Hyper-V мешает VMware и VirtualBox работать на полной скорости и не даёт отключить виртуализацию безопасности.",
      "Microsoft's hypervisor. With Hyper-V on, VMware and VirtualBox cannot run at full speed and memory integrity cannot be turned off.",
      "keep", "virt"),

    f("VirtualMachinePlatform", "Платформа виртуальной машины", "Virtual Machine Platform",
      "Нужна для WSL 2 и Android-подсистемы. Без неё WSL 2 не запустится.",
      "Required by WSL 2 and the Android subsystem. WSL 2 will not start without it.",
      "keep", "virt"),

    f("Microsoft-Windows-Subsystem-Linux", "Подсистема Windows для Linux", "Windows Subsystem for Linux",
      "WSL. Если дистрибутивы Linux не установлены, компонент просто занимает место.",
      "WSL. With no Linux distributions installed the component just takes space.",
      "keep", "virt"),

    f("SearchEngine-Client-Package", "Служба индексации Windows Search", "Windows Search engine",
      "Индекс поиска. Отключение здесь ломает поиск в Пуске и в проводнике целиком, а не просто замедляет его — для этого есть отдельный твик поспокойнее.",
      "The search index. Turning it off here breaks search in Start and Explorer entirely rather than just slowing it down — there is a gentler tweak for that.",
      "risky", "system"),

    f("Client-ProjFS", "Проекция файловой системы", "Projected File System",
      "Виртуальная файловая система, которую использует Git VFS в очень больших репозиториях.",
      "A virtual file system used by Git VFS on very large repositories.",
      "off", "corp"),
]

problems = []
seen = set()
for item in ITEMS:
    if item["name"] in seen:
        problems.append("duplicate feature: " + item["name"])
    seen.add(item["name"])
    if item["rec"] not in ALLOWED_REC:
        problems.append("%s: bad rec %s" % (item["name"], item["rec"]))
    for key in ("ru", "en", "dru", "den"):
        if not item[key].strip():
            problems.append("%s: empty %s" % (item["name"], key))

if problems:
    for p in problems:
        print("PROBLEM " + p)
    raise SystemExit(1)

db = {"version": 1, "features": ITEMS}
os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, "w", encoding="utf-8") as out:
    json.dump(db, out, ensure_ascii=False, separators=(",", ":"))

groups = {}
for item in ITEMS:
    groups[item["group"]] = groups.get(item["group"], 0) + 1
print("features: %d" % len(ITEMS))
print("groups: " + ", ".join("%s=%d" % kv for kv in sorted(groups.items())))
print("written: %s (%d bytes)" % (os.path.normpath(OUT), os.path.getsize(OUT)))
