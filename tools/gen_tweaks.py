import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import tweaks_a
import tweaks_b
import tweaks_c
import tweaks_d

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "KlondaikTweaker", "Assets", "Data", "tweaks.json")

ALLOWED_RISK = {"safe", "advanced", "extreme"}
ALLOWED_KIND = {"reg", "regdel", "svc", "task", "appx", "cmd", "hosts"}
ALLOWED_HIVE = {"HKLM", "HKCU", "HKCR", "HKU"}
ALLOWED_TYPE = {"dword", "qword", "sz", "expand", "multi", "binary"}

items = tweaks_a.ITEMS + tweaks_b.ITEMS + tweaks_c.ITEMS + tweaks_d.ITEMS

seen = set()
problems = []
for t in items:
    if t["id"] in seen:
        problems.append("duplicate id: " + t["id"])
    seen.add(t["id"])
    if t["risk"] not in ALLOWED_RISK:
        problems.append(t["id"] + ": bad risk " + t["risk"])
    for field in ("ru", "en"):
        if not t[field]["t"] or not t[field]["d"]:
            problems.append(t["id"] + ": empty " + field)
    if not t["actions"]:
        problems.append(t["id"] + ": no actions")
    for a in t["actions"]:
        if a["k"] not in ALLOWED_KIND:
            problems.append(t["id"] + ": bad kind " + a["k"])
        if a["k"] in ("reg", "regdel"):
            if a.get("h") not in ALLOWED_HIVE:
                problems.append(t["id"] + ": bad hive " + str(a.get("h")))
            if not a.get("p"):
                problems.append(t["id"] + ": no path")
        if a["k"] == "reg" and a.get("t") not in ALLOWED_TYPE:
            problems.append(t["id"] + ": bad type " + str(a.get("t")))
        if a["k"] == "cmd" and not a.get("exe"):
            problems.append(t["id"] + ": no exe")

if problems:
    for p in problems:
        print("PROBLEM " + p)
    sys.exit(1)

db = {"version": 1, "tweaks": items}
os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, "w", encoding="utf-8") as f:
    json.dump(db, f, ensure_ascii=False, separators=(",", ":"))

cats = {}
risks = {}
for t in items:
    cats[t["cat"]] = cats.get(t["cat"], 0) + 1
    risks[t["risk"]] = risks.get(t["risk"], 0) + 1

print("tweaks: %d" % len(items))
print("categories: " + ", ".join("%s=%d" % kv for kv in sorted(cats.items())))
print("risk: " + ", ".join("%s=%d" % kv for kv in sorted(risks.items())))
print("written: %s (%d bytes)" % (os.path.normpath(OUT), os.path.getsize(OUT)))
