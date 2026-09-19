import io
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "service_defaults.txt")
OUT = os.path.join(HERE, "..", "src", "KlondaikTweaker", "Assets", "Data", "repair.json")

defaults = {}
for line in io.open(SRC, encoding="utf-8"):
    line = line.strip()
    if not line or "=" not in line:
        continue
    name, mode = line.split("=", 1)
    if mode in ("boot", "system", "auto", "manual", "disabled"):
        defaults[name] = mode

critical = [
    "BFE", "mpssvc", "Dnscache", "nsi", "NlaSvc", "Dhcp", "netprofm", "RpcSs",
    "DcomLaunch", "LSM", "Power", "ProfSvc", "Schedule", "gpsvc", "EventLog",
    "CryptSvc", "TrustedInstaller", "msiserver", "WinDefend", "wscsvc",
    "SecurityHealthService", "Spooler", "AudioSrv", "Audiosrv", "AudioEndpointBuilder",
    "BrokerInfrastructure", "SystemEventsBroker", "UserManager", "Themes", "ShellHWDetection",
]

db = {
    "serviceDefaults": defaults,
    "critical": sorted(set(critical) & set(defaults)) or critical,
}

os.makedirs(os.path.dirname(OUT), exist_ok=True)
io.open(OUT, "w", encoding="utf-8").write(json.dumps(db, ensure_ascii=False, separators=(",", ":")))
print("services: %d" % len(defaults))
print("written: %s (%d bytes)" % (os.path.normpath(OUT), os.path.getsize(OUT)))
