CV = r"SOFTWARE\Microsoft\Windows\CurrentVersion"
CVU = r"Software\Microsoft\Windows\CurrentVersion"
POL = r"SOFTWARE\Policies\Microsoft\Windows"
POLU = r"Software\Policies\Microsoft\Windows"
CCS = r"SYSTEM\CurrentControlSet"
ADV = CVU + r"\Explorer\Advanced"
MM = CCS + r"\Control\Session Manager\Memory Management"
MMPROF = r"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"


def R(h, p, n, t, v, d=None, dk=False):
    a = {"k": "reg", "h": h, "p": p, "n": n, "t": t, "v": str(v)}
    if d is not None:
        a["d"] = str(d)
    if dk:
        a["dk"] = True
    return a


def RD(h, p, n=None):
    a = {"k": "regdel", "h": h, "p": p}
    if n is not None:
        a["n"] = n
    return a


def SV(n, v="disabled", d=None, stop=True):
    a = {"k": "svc", "n": n, "v": v}
    if d is not None:
        a["d"] = d
    if stop:
        a["stop"] = True
    return a


def TK(p, v="off"):
    return {"k": "task", "p": p, "v": v}


def CM(exe, args, rargs=None):
    a = {"k": "cmd", "exe": exe, "args": args}
    if rargs is not None:
        a["rargs"] = rargs
    return a


def PS(cmd, rcmd=None):
    return CM("powershell.exe", "-NoProfile -NonInteractive -Command " + cmd,
              ("-NoProfile -NonInteractive -Command " + rcmd) if rcmd else None)


def AX(n):
    return {"k": "appx", "n": n}


def HS(domains):
    return {"k": "hosts", "v": "|".join(domains)}


def T(tid, cat, risk, ru, en, actions, tags=(), src=None, req=None, restart=False, logoff=False):
    item = {
        "id": tid,
        "cat": cat,
        "risk": risk,
        "tags": list(tags),
        "ru": {"t": ru[0], "d": ru[1]},
        "en": {"t": en[0], "d": en[1]},
        "actions": actions,
    }
    if len(ru) > 2 and ru[2]:
        item["ru"]["w"] = ru[2]
    if len(en) > 2 and en[2]:
        item["en"]["w"] = en[2]
    if src:
        item["src"] = src
    if req:
        item["req"] = req
    if restart:
        item["restart"] = True
    if logoff:
        item["logoff"] = True
    return item
