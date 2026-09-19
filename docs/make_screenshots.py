import http.server
import os
import shutil
import socket
import socketserver
import subprocess
import sys
import tempfile
import threading
import time
from pathlib import Path
from urllib.parse import quote

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / "src" / "KlondaikTweaker" / "Assets"
DOCS = ROOT / "docs"

WIDTH = 1560
HEIGHT = 980

SHOTS = [
    ("main", "dash", []),
    ("wizard", "wizard", ['[data-go="start"]']),
    ("tweaks", "tweaks", []),
    ("tools", "tools", []),
]

BROWSERS = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
]


def find_browser():
    for path in BROWSERS:
        if Path(path).exists():
            return path
    raise SystemExit("no Chrome or Edge found, install one or edit BROWSERS")


def free_port():
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


def stage(target):
    for name in ("css", "js", "fonts", "vendor"):
        shutil.copytree(ASSETS / "wwwroot" / name, target / name)
    shutil.copy(ASSETS / "wwwroot" / "logo.png", target)
    shutil.copytree(ASSETS / "Data", target / "data")
    shutil.copy(DOCS / "mock.js", target)

    html = (ASSETS / "wwwroot" / "index.html").read_text(encoding="utf-8")
    start = html.find('<meta http-equiv="Content-Security-Policy"')
    if start != -1:
        end = html.find(">", start) + 1
        html = html[:start] + "<!-- csp removed for screenshots -->" + html[end:]
    html = html.replace(
        '<script type="module" src="js/app.js">',
        '<script src="mock.js"></script>\n    <script type="module" src="js/app.js">',
    )
    (target / "index.html").write_text(html, encoding="utf-8")


def serve(directory, port):
    class Handler(http.server.SimpleHTTPRequestHandler):
        def __init__(self, *args, **kwargs):
            super().__init__(*args, directory=str(directory), **kwargs)

        def log_message(self, *args):
            pass

    server = socketserver.TCPServer(("127.0.0.1", port), Handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    return server


def shoot(browser, profile, url, out):
    subprocess.run(
        [
            browser,
            "--headless=new",
            "--no-sandbox",
            "--hide-scrollbars",
            "--force-device-scale-factor=1",
            f"--user-data-dir={profile}",
            f"--window-size={WIDTH},{HEIGHT}",
            "--virtual-time-budget=12000",
            f"--screenshot={out}",
            url,
        ],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        check=True,
    )
    if not Path(out).exists():
        raise SystemExit(f"browser produced nothing for {url}")


def main():
    browser = find_browser()
    port = free_port()
    work = Path(tempfile.mkdtemp(prefix="klondaik-shots-"))
    try:
        site = work / "site"
        site.mkdir()
        stage(site)
        server = serve(site, port)
        time.sleep(0.5)

        for lang in ("ru", "en"):
            for name, page, clicks in SHOTS:
                query = f"accept=1&still=1&lang={lang}&p={page}"
                for selector in clicks:
                    query += "&click=" + quote(selector, safe="")
                suffix = "" if lang == "ru" else "-en"
                out = DOCS / f"screenshot-{name}{suffix}.png"
                profile = work / f"profile-{lang}-{name}"
                shoot(browser, profile, f"http://127.0.0.1:{port}/index.html?{query}", out)
                print(f"{out.name}  {out.stat().st_size // 1024} KB")

        server.shutdown()
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    if os.name != "nt":
        print("the interface is built for Windows, but the shots only need a Chromium browser")
    main()
