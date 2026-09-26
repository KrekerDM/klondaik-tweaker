# -*- coding: utf-8 -*-
import io
import os
import re
import sys

BASE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "KlondaikTweaker")
JS = os.path.join(BASE, "Assets", "wwwroot", "js")
LANG = os.path.join(JS, "lang")

FALSE_KEYS = re.compile(r'^(button|canvas|select|template|option|div|span|other)$|^\.')


def read(path):
    return io.open(path, encoding="utf-8").read()


def keys(path):
    return set(re.findall(r'^\s*"([A-Za-z0-9_.]+)":', read(path), re.M))


def main():
    fails = []

    files = sorted(f[:-3] for f in os.listdir(LANG) if f.endswith(".js"))
    i18n = read(os.path.join(JS, "i18n.js"))
    listed = re.findall(r'id:\s*"([a-z]{2})"', i18n)
    imported = re.findall(r'import\s+([a-z]{2})\s+from\s+"\./lang/([a-z]{2})\.js"', i18n)
    texts = read(os.path.join(BASE, "Core", "Engine", "Texts.cs"))
    backend = re.findall(r'"([a-z]{2})"', re.search(r'Languages\s*=\s*new\([^)]*\)\s*\{([^}]*)\}',
                                                   read(os.path.join(BASE, "Host", "Api.cs")), re.S).group(1))

    if set(files) != set(listed):
        fails.append("i18n.js lists %s, the lang folder holds %s" % (sorted(listed), files))
    if len(listed) != len(set(listed)):
        fails.append("i18n.js lists a language twice: %s" % listed)
    if set(backend) != set(files):
        fails.append("Api.Languages is %s, the lang folder holds %s" % (sorted(backend), files))
    if any(a != b for a, b in imported) or len(imported) != len(files):
        fails.append("i18n.js imports do not line up with the dictionaries: %s" % imported)

    latin_js = re.findall(r'"([a-z]{2})"', re.search(r'LATIN\s*=\s*new Set\(\[([^\]]*)\]', i18n).group(1))
    latin_cs = re.findall(r'"([a-z]{2})"', re.search(r'LatinText\s*=\s*new\([^)]*\)\s*\{([^}]*)\}', texts, re.S).group(1))
    if set(latin_js) != set(latin_cs):
        fails.append("the text-language rule differs: js %s, C# %s" % (sorted(latin_js), sorted(latin_cs)))
    if not set(latin_js) <= set(files):
        fails.append("the text-language rule names a language with no dictionary: %s" % sorted(set(latin_js) - set(files)))

    base = keys(os.path.join(LANG, "ru.js"))
    for code in files:
        table = keys(os.path.join(LANG, code + ".js"))
        missing = sorted(base - table)
        extra = sorted(table - base)
        if missing:
            fails.append("%s.js is missing %d keys: %s" % (code, len(missing), missing[:6]))
        if extra:
            fails.append("%s.js has %d keys ru.js does not: %s" % (code, len(extra), extra[:6]))

    used = set()
    for folder, _, names in os.walk(JS):
        if os.path.basename(folder) == "lang":
            continue
        for name in names:
            if name.endswith(".js"):
                used |= set(re.findall(r'[^A-Za-z_.]t\(\s*"([A-Za-z0-9_.]+)"\s*\)', read(os.path.join(folder, name))))
    absent = sorted(k for k in used - base if not FALSE_KEYS.match(k))
    if absent:
        fails.append("the interface asks for keys no dictionary has: %s" % absent)

    for problem in fails:
        print(problem)
    if fails:
        print()
        print("%d language problems" % len(fails))
        return 1
    print("%d dictionaries, %d keys each, one shared list of languages" % (len(files), len(base)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
