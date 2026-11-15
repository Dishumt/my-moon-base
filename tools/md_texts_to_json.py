# AI-assisted: written with Claude. See docs/AI_USE.md
"""
Moves player texts from the roles' md files into the game text tables.

    python tools/md_texts_to_json.py           -> adds missing keys to texts_en.json / texts_uk.json
    python tools/md_texts_to_json.py --check   -> only shows what it would do, writes nothing

Rules:
  * a key that is already in the JSON is never overwritten; if the md text differs, it is printed as a conflict;
  * safe to run again and again (second run adds nothing);
  * a key that role 04 already has in Launch/texts_launch_*.json is not copied: Loc reads that table too
    (04 keeps its newest texts there; its md can be older);
  * at the end it lists keys that the C# code or Data/*.json use, but no text table has.

Sources:
  Scripts/Simulation/TEXTS_FOR_05.md, Scripts/Logistics/TEXTS_FOR_05.md
      sections "## texts_en.json" / "## texts_uk.json" with ```json blocks of { "key": "...", "text": "..." } lines
  Scripts/Radiation/FOR_OTHER_ROLES.md
      table rows | key | EN | UK |
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJECT = os.path.join(ROOT, "Assets", "_Project")
SCRIPTS = os.path.join(PROJECT, "Scripts")
DATA = os.path.join(PROJECT, "Resources", "Data")
TEXT_FILES = {
    "en": os.path.join(DATA, "Text", "texts_en.json"),
    "uk": os.path.join(DATA, "Text", "texts_uk.json"),
}
LAUNCH_FILES = {
    "en": os.path.join(DATA, "Launch", "texts_launch_en.json"),
    "uk": os.path.join(DATA, "Launch", "texts_launch_uk.json"),
}
JSON_BLOCK_SOURCES = [
    os.path.join(SCRIPTS, "Simulation", "TEXTS_FOR_05.md"),
    os.path.join(SCRIPTS, "Logistics", "TEXTS_FOR_05.md"),
]
TABLE_SOURCES = [
    os.path.join(SCRIPTS, "Radiation", "FOR_OTHER_ROLES.md"),
]

KEY_RE = re.compile(r"^[a-z][a-z0-9_]*(\.[A-Za-z0-9_]+)+$")
NOT_KEY_ENDINGS = (".json", ".uxml", ".uss", ".tss", ".png", ".asset", ".unity", ".cs", ".mat",
                   ".md", ".txt", ".gov", ".com", ".org", ".net", ".io", ".edu", ".html")


def rel(path):
    return os.path.relpath(path, ROOT).replace("\\", "/")


def read_text(path):
    with open(path, "r", encoding="utf-8-sig") as f:
        return f.read()


# ---------- reading md ----------

def parse_json_block_md(path):
    """{lang: [(key, text)]} from '## texts_en.json' / '## texts_uk.json' sections."""
    result = {"en": [], "uk": []}
    lang = None
    in_block = False
    for n, raw in enumerate(read_text(path).splitlines(), 1):
        line = raw.strip()
        if line.startswith("## "):
            m = re.match(r"##\s+texts_(en|uk)\.json", line)
            lang = m.group(1) if m else None
            in_block = False
            continue
        if line.startswith("```"):
            in_block = (not in_block) and lang is not None
            continue
        if not in_block or not line.startswith("{"):
            continue
        try:
            obj = json.loads(line.rstrip(","))
            result[lang].append((obj["key"], obj["text"]))
        except (ValueError, KeyError):
            print("  ! cannot read line %s:%d: %s" % (rel(path), n, line))
    return result


def split_row(line):
    cells = re.split(r"(?<!\\)\|", line.strip().strip("|"))
    return [c.strip().replace("\\|", "|") for c in cells]


def parse_table_md(path):
    """{lang: [(key, text)]} from table rows '| key | EN | UK |'."""
    result = {"en": [], "uk": []}
    in_table = False
    for raw in read_text(path).splitlines():
        line = raw.strip()
        if not line.startswith("|"):
            in_table = False
            continue
        cells = split_row(line)
        if len(cells) >= 3 and [c.lower() for c in cells[:3]] == ["key", "en", "uk"]:
            in_table = True
            continue
        if not in_table or set("".join(cells)) <= set("-: "):
            continue
        if len(cells) < 3:
            continue
        key = cells[0].strip("`").strip()
        if KEY_RE.match(key):
            result["en"].append((key, cells[1]))
            result["uk"].append((key, cells[2]))
    return result


# ---------- text tables ----------

def load_table(path):
    if not os.path.exists(path):
        return {}
    data = json.loads(read_text(path))
    return {e["key"]: e["text"] for e in data.get("items", [])}


def append_items(path, new_items):
    """Adds lines '    { "key": ..., "text": ... }' before the closing ']' and keeps the file style."""
    with open(path, "rb") as f:
        raw = f.read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    newline = "\r\n" if "\r\n" in text else "\n"
    lines = text.split(newline)
    close = max(i for i, l in enumerate(lines) if l.strip() == "]")
    last = close - 1
    while last >= 0 and lines[last].strip() == "":
        last -= 1
    if lines[last].strip().startswith("{") and not lines[last].rstrip().endswith(","):
        lines[last] = lines[last].rstrip() + ","
    added = []
    for i, (key, value) in enumerate(new_items):
        comma = "," if i < len(new_items) - 1 else ""
        added.append('    { "key": %s, "text": %s }%s' % (
            json.dumps(key, ensure_ascii=False), json.dumps(value, ensure_ascii=False), comma))
    lines[last + 1:close] = added
    out = newline.join(lines)
    json.loads(out)  # never write broken JSON
    with open(path, "wb") as f:
        f.write((b"\xef\xbb\xbf" if bom else b"") + out.encode("utf-8"))


# ---------- keys used in code / data ----------

def code_keys():
    """{key: [file:line]} for string literals that look like text keys."""
    found = {}
    lit = re.compile(r'"((?:[^"\\\n]|\\.)*)"')
    for folder, _, files in os.walk(SCRIPTS):
        if os.sep + "Editor" in folder:
            continue
        for name in files:
            if not name.endswith(".cs"):
                continue
            path = os.path.join(folder, name)
            for n, line in enumerate(read_text(path).splitlines(), 1):
                if line.strip().startswith("//"):
                    continue
                for m in lit.finditer(line):
                    s = m.group(1)
                    if re.match(r"\s*\+", line[m.end():]):
                        continue  # "flight.step" + n: only the start of a key
                    if KEY_RE.match(s) and not s.lower().endswith(NOT_KEY_ENDINGS):
                        found.setdefault(s, []).append("%s:%d" % (rel(path), n))
    return found


def data_keys():
    """{key: [file]} for values of '*Key' fields in Resources/Data/**/*.json (except the text tables)."""
    found = {}

    def walk(node, path):
        if isinstance(node, dict):
            for k, v in node.items():
                if isinstance(v, str) and k.lower().endswith("key") and k != "key" and KEY_RE.match(v):
                    found.setdefault(v, []).append(rel(path))
                else:
                    walk(v, path)
        elif isinstance(node, list):
            for v in node:
                walk(v, path)

    for folder, _, files in os.walk(DATA):
        for name in files:
            if not name.endswith(".json") or name.startswith("texts_"):
                continue
            path = os.path.join(folder, name)
            try:
                walk(json.loads(read_text(path)), path)
            except ValueError:
                print("  ! not valid JSON: " + rel(path))
    return found


# ---------- main ----------

def main():
    check_only = "--check" in sys.argv
    print("Texts: md -> texts_en.json / texts_uk.json" + ("  (CHECK ONLY, nothing is written)" if check_only else ""))

    wanted = {"en": [], "uk": []}
    origin = {}
    for path in JSON_BLOCK_SOURCES + TABLE_SOURCES:
        if not os.path.exists(path):
            print("  - no file " + rel(path) + " (skipped)")
            continue
        parsed = parse_json_block_md(path) if path in JSON_BLOCK_SOURCES else parse_table_md(path)
        print("  %s: %d EN, %d UK" % (rel(path), len(parsed["en"]), len(parsed["uk"])))
        for lang in ("en", "uk"):
            for key, value in parsed[lang]:
                wanted[lang].append((key, value, rel(path)))
                origin.setdefault(key, rel(path))

    tables = {lang: load_table(p) for lang, p in TEXT_FILES.items()}
    launch = {lang: load_table(p) for lang, p in LAUNCH_FILES.items()}
    problems = 0
    for lang in ("en", "uk"):
        table = tables[lang]
        new, seen, in_launch = [], {}, 0
        for key, value, src in wanted[lang]:
            if key in seen:
                if seen[key] != value:
                    print("  ! %s %s: two different texts in md (%s) - first one kept" % (lang, key, src))
                    problems += 1
                continue
            seen[key] = value
            if key not in table and key in launch[lang]:
                in_launch += 1
                continue
            if key in table:
                if table[key] != value:
                    print("  ~ %s %s: JSON already has another text (kept JSON)\n      JSON: %s\n      md:   %s"
                          % (lang, key, table[key], value))
                continue
            new.append((key, value))
        only_one = set(seen) ^ set(k for k, _, _ in wanted["uk" if lang == "en" else "en"])
        for key in sorted(only_one):
            if key in seen and key not in tables["uk" if lang == "en" else "en"]:
                print("  ! %s has no %s text in md: %s" % (key, "UK" if lang == "en" else "EN", origin.get(key)))
                problems += 1
        print("  %s: %d keys now, %d to add (%d more are already in 04's Launch table)"
              % (rel(TEXT_FILES[lang]), len(table), len(new), in_launch))
        if new and not check_only:
            append_items(TEXT_FILES[lang], new)
            for key, value in new:
                table[key] = value

    # keys that code or data use, but no table has
    used = code_keys()
    for key, where in data_keys().items():
        used.setdefault(key, []).extend(where)
    missing = []
    for key in sorted(used):
        have_en = key in tables["en"] or key in launch["en"]
        have_uk = key in tables["uk"] or key in launch["uk"]
        if not (have_en and have_uk):
            missing.append((key, "EN" if not have_en and have_uk else "UK" if have_en else "EN+UK", used[key][0]))
    for lang in ("en", "uk"):
        other = "uk" if lang == "en" else "en"
        for key in sorted(set(tables[lang]) - set(tables[other])):
            print("  ! key only in %s: %s" % (rel(TEXT_FILES[lang]), key))
            problems += 1
        for key in sorted(set(tables[lang]) & set(launch[lang])):
            if tables[lang][key] != launch[lang][key]:
                print("  ! %s %s: main table and 04's Launch table have different texts; Loc shows the main one\n"
                      "      main:   %s\n      launch: %s" % (lang, key, tables[lang][key], launch[lang][key]))

    if missing:
        print("\nKeys used in code/data with no text (%d). Literals only: keys built with '+' are not seen." % len(missing))
        for key, lacks, where in missing:
            print("  - %-40s needs %-5s  %s" % (key, lacks, where))
    else:
        print("\nEvery key literal in code/data has a text.")
    print("\nDone. Problems: %d" % problems)
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
