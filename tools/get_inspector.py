import io
import json
import os
import shutil
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass
import urllib.request
import zipfile

REPO = "Orbmu2k/nvidiaProfileInspector"
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "src", "KlondaikTweaker", "Assets", "vendor")
EXE = "nvidiaProfileInspector.exe"
MIN_SIZE = 200 * 1024
MAX_SIZE = 20 * 1024 * 1024


def fetch(url, accept="application/vnd.github+json"):
    request = urllib.request.Request(url, headers={
        "User-Agent": "KlondaikTweaker-build",
        "Accept": accept,
    })
    with urllib.request.urlopen(request, timeout=120) as response:
        return response.read()


def main():
    print("запрашиваю последний релиз " + REPO)
    release = json.loads(fetch(f"https://api.github.com/repos/{REPO}/releases/latest"))
    tag = release.get("tag_name", "?")

    asset = None
    for candidate in release.get("assets", []):
        if candidate["name"].lower().endswith(".zip"):
            asset = candidate
            break
    if asset is None:
        raise SystemExit("в релизе нет zip-архива")

    print(f"версия {tag}, файл {asset['name']}, {asset['size'] // 1024} КБ")
    blob = fetch(asset["browser_download_url"], accept="application/octet-stream")

    exe_bytes = None
    license_bytes = None
    with zipfile.ZipFile(io.BytesIO(blob)) as archive:
        for name in archive.namelist():
            base = os.path.basename(name).lower()
            if base == EXE.lower():
                exe_bytes = archive.read(name)
            elif base in ("license", "license.txt") and license_bytes is None:
                license_bytes = archive.read(name)

    if exe_bytes is None:
        raise SystemExit("в архиве нет " + EXE)
    if not exe_bytes.startswith(b"MZ"):
        raise SystemExit("скачанный файл не похож на программу Windows")
    if not MIN_SIZE <= len(exe_bytes) <= MAX_SIZE:
        raise SystemExit(f"подозрительный размер: {len(exe_bytes)} байт")

    if license_bytes is None:
        print("предупреждение: в архиве нет файла лицензии, беру из репозитория")
        license_bytes = fetch(
            f"https://raw.githubusercontent.com/{REPO}/master/LICENSE",
            accept="text/plain",
        )

    shutil.rmtree(OUT, ignore_errors=True)
    os.makedirs(OUT, exist_ok=True)

    with open(os.path.join(OUT, EXE), "wb") as out:
        out.write(exe_bytes)
    with open(os.path.join(OUT, "nvidiaProfileInspector-LICENSE.txt"), "wb") as out:
        out.write(license_bytes)
    with open(os.path.join(OUT, "version.txt"), "w", encoding="utf-8") as out:
        out.write(tag)

    print(f"положено в {os.path.normpath(OUT)}: {EXE} ({len(exe_bytes) // 1024} КБ) и лицензия")


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print("ОШИБКА: " + str(error))
        sys.exit(1)
