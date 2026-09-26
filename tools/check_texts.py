# -*- coding: utf-8 -*-
import io
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "KlondaikTweaker")

CONSOLE_ONLY = {
    "Program.cs",
    "SelfTest.cs",
    "ModuleTest.cs",
    "PerfTest.cs",
    "IrqDump.cs",
    "NicDump.cs",
}

BILINGUAL = ("Texts.Pick(", "PrefersEnglish", "Ru ?", "NoteRu", "NoteEn", 'lang == "en"')

LOW = chr(0x400)
HIGH = chr(0x4FF)


def russian(text):
    return any(LOW <= c <= HIGH for c in text)


def literals(text):
    return re.findall(r'"((?:[^"\\]|\\.)*)"', text)


def blocks(path):
    text = io.open(path, encoding="utf-8").read()
    out = []
    start = 0
    for i, ch in enumerate(text):
        if ch in ";{}":
            out.append((text.count("\n", 0, start) + 1, text[start:i + 1]))
            start = i + 1
    out.append((text.count("\n", 0, start) + 1, text[start:]))
    return out


def problems(path):
    found = []
    for line_no, chunk in blocks(path):
        russian_strings = [s for s in literals(chunk) if russian(s)]
        if not russian_strings:
            continue
        if any(mark in chunk for mark in BILINGUAL):
            continue
        if re.search(r'(Contains|StartsWith|EndsWith|Equals|IndexOf)\s*\(\s*"', chunk):
            continue
        if [s for s in literals(chunk) if not russian(s) and re.search(r'[A-Za-z]{4}', s)]:
            continue
        head = chunk.strip().splitlines()[0][:120] if chunk.strip() else ""
        found.append((line_no, russian_strings[0], head))
    return found


def main():
    total = 0
    for folder, _, files in os.walk(ROOT):
        parts = folder.split(os.sep)
        if "obj" in parts or "bin" in parts or "Assets" in parts:
            continue
        for name in sorted(files):
            if not name.endswith(".cs") or name in CONSOLE_ONLY:
                continue
            path = os.path.join(folder, name)
            rel = os.path.relpath(path, ROOT).replace(os.sep, "/")
            for line_no, text, head in problems(path):
                total += 1
                print("%s:%d  no English variant: %s" % (rel, line_no, text[:70]))
                print("    %s" % head)
    if total:
        print()
        print("%d strings would reach a non-Russian user in Russian" % total)
        print("wrap them in Texts.Pick(ru, en)")
        return 1
    print("every user-facing string in the backend carries both languages")
    return 0


if __name__ == "__main__":
    sys.exit(main())
