#!/usr/bin/env python3
"""Player-text audit scanner (M11-31). Stdlib only.

Extracts player-visible strings from the C# UI code, locale.toml files, archive TOML, briefings and data/ui text,
flags dev-looking text, and writes a Markdown report.

    python3 tools/text/scan_text.py                       # print counts and the flagged list
    python3 tools/text/scan_text.py --write FILE [--section "Before"|"After"]
    python3 tools/text/scan_text.py --counts              # one line per category (used by the audit doc)
"""
import argparse
import os
import re
import sys
from collections import OrderedDict, defaultdict

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

CS_DIRS = ["src/Game/Ui", "src/Game/Screens", "src/Game/Archive", "src/Game/Campaign", "src/Game/Veterancy", "src/Game/Menu"]
CS_FILES = ["src/Game/Controls/ActionInfo.cs", "src/Game/Controls/KeyNames.cs"]
# Settings.cs only writes the settings file; SceneSet.cs and ArchiveCatalog.cs only produce loader warnings for the log.

# Files whose strings never reach a normal player: listed under "debug only", not counted.
DEBUG_FILES = {"DebugConsole.cs", "DebugCommandParser.cs", "MissionDebugLine.cs"}
DEBUG_LINE = re.compile(r"[Dd]iagnostics|\bhash\b.*\bfps\b|\bfps\b|debug line|\[debug\]|DebugLine", re.I)
# Source that is never drawn: log/exception/assert/path plumbing, editor-only screens.
SKIP_FILES = {"CameoNames.cs", "DisplayNames.cs", "MissionObjectiveLines.cs", "AppLog.cs", "SceneSet.cs", "ArchiveCatalog.cs"}
SKIP_DIRS = ("src/Game/Screens/Editor/", "src/Game/Ui/Objectives/")   # objectives: owned by another helper
NONTEXT_CALL = re.compile(r"\b(Log|AppLog\.\w+|Console\.\w+|Trace\.\w+|throw new \w+|Debug\.\w+|Assert\w*|Path\.\w+|File\.\w+|"
                          r"Directory\.\w+|InvalidDataException|ArgumentException|InvalidOperationException|"
                          r"ArgumentNullException|FormatException|ArgumentOutOfRangeException|NotSupportedException|"
                          r"TryGetValue|ContainsKey|StartsWith|EndsWith|Environment\.\w+|GeneratedRegex|Regex)\b")

CATEGORIES = OrderedDict([
    ("raw_id", "Raw ids shown to players (snake_case ids, type ids, interpolated ids)"),
    ("dotted_key", "Dotted keys shown to players"),
    ("placeholder_bracket", "Bracketed placeholders ([draft pending], [key])"),
    ("dev_wording", "TODO / draft / placeholder / test / stub wording"),
    ("punctuation", "Stray double periods or '...'"),
    ("capitalisation", "Inconsistent capitalisation of unit/faction names vs archive titles"),
    ("programmer_word", "Programmer words in player text (null, id, enum, tick, index, debug, sim, cell coordinates)"),
])

FILE_EXT = re.compile(r"\.(toml|png|json|ogg|wav|lua|md|txt|cs|csv|tsv|ttf|fnt|xml|log|exe|dll|html|ini|bin|sav|replay|rpl|jpg|zip|mp3|oga)$", re.I)
SNAKE = re.compile(r"(?<![A-Za-z0-9_.{])[a-z][a-z0-9]*(?:_[a-z0-9]+)+(?![A-Za-z0-9_])")
DOTTED = re.compile(r"^[a-z][a-z0-9]*(?:\.[a-z0-9_]+)+$")
BRACKET = re.compile(r"\[[A-Za-z][^\]\[]*\]")
KEY_LABEL = re.compile(r"^\[(?:[A-Z][A-Za-z0-9+ ]{0,12}|\d+)\]$")  # [F1], [Esc], [Ctrl+S]: key hints are fine
DEVWORD = re.compile(r"\b(TODO|FIXME|XXX|TBD|WIP|draft|placeholder|stub|lorem|dummy|not wired|wired yet|not implemented|not written|coming soon|assumed|test (?:connection|failed|only)|for testing)\b", re.I)
DOUBLEDOT = re.compile(r"\.\.")
PROGWORD = re.compile(r"\b(null|ids?|enums?|ticks?|index|indices|debug|sims?|cell coordinates|cell coords?|nan|exception|bool|int|json|toml|struct|bitmask|hash)\b", re.I)
ID_HOLE = re.compile(r"\{[^{}:]*\b(?:\w*Id|id|\w*TypeId|RulesId|TechId|TypeName|Key)\b[^{}:]*\}")


def rel(p):
    return os.path.relpath(p, ROOT).replace(os.sep, "/")


# ---------------------------------------------------------------- C# literal extraction
def cs_literals(src):
    """Yield (line, kind, text, holes) for every string literal outside comments. Interpolation holes are kept as {expr}."""
    i, n, line = 0, len(src), 1
    out = []
    while i < n:
        c = src[i]
        if c == "\n":
            line += 1; i += 1; continue
        if src.startswith("//", i):
            j = src.find("\n", i)
            i = n if j < 0 else j
            continue
        if src.startswith("/*", i):
            j = src.find("*/", i + 2)
            j = n if j < 0 else j + 2
            line += src.count("\n", i, j); i = j; continue
        if c == "'":
            j = i + 1
            while j < n and src[j] != "'":
                j += 2 if src[j] == "\\" else 1
            i = j + 1; continue
        # raw string  """..."""  (optionally $-prefixed)
        m = re.match(r'\$*"""', src[i:i + 8])
        if m:
            q = src.find('"""', i + len(m.group(0)))
            q = n if q < 0 else q
            body = src[i + len(m.group(0)):q]
            out.append((line, "raw", body.strip("\n"), "$" in m.group(0)))
            line += src.count("\n", i, q + 3); i = q + 3; continue
        m = re.match(r'(\$@|@\$|\$|@)?"', src[i:i + 3])
        if m:
            prefix = m.group(1) or ""
            verbatim = "@" in prefix
            interp = "$" in prefix
            j = i + len(m.group(0))
            buf = []
            depth = 0
            start_line = line
            while j < n:
                ch = src[j]
                if interp and ch == "{" and src.startswith("{{", j) and depth == 0:
                    buf.append("{"); j += 2; continue
                if interp and ch == "}" and src.startswith("}}", j) and depth == 0:
                    buf.append("}"); j += 2; continue
                if interp and ch == "{" and depth == 0:
                    depth = 1; buf.append(ch); j += 1; continue
                if interp and depth > 0:
                    # inside a hole: strings in the hole may contain quotes; track braces and nested literals crudely
                    if ch == "{": depth += 1
                    elif ch == "}": depth -= 1
                    elif ch == '"':
                        k = j + 1
                        while k < n and src[k] != '"':
                            k += 2 if src[k] == "\\" else 1
                        buf.append(src[j:k + 1]); j = k + 1; continue
                    buf.append(ch); j += 1; continue
                if ch == "\n":
                    line += 1
                if verbatim:
                    if ch == '"' and src.startswith('""', j):
                        buf.append('"'); j += 2; continue
                    if ch == '"':
                        break
                else:
                    if ch == "\\" and j + 1 < n:
                        e = src[j + 1]
                        buf.append({"n": "\n", "t": "\t", '"': '"', "\\": "\\", "'": "'"}.get(e, e)); j += 2; continue
                    if ch == '"':
                        break
                    if ch == "\n":
                        break
                buf.append(ch); j += 1
            out.append((start_line, "interp" if interp else "plain", "".join(buf), interp))
            i = j + 1; continue
        i += 1
    return out


def has_letters(s):
    return re.search(r"[A-Za-z]{2}", s) is not None


# ---------------------------------------------------------------- archive titles
def read_archive():
    titles = {}   # id -> title
    entries = []  # (path, line, field, value, id)
    adir = os.path.join(ROOT, "data/archive")
    for fn in sorted(os.listdir(adir)):
        if not fn.endswith(".toml"):
            continue
        path = os.path.join(adir, fn)
        cur = None
        for ln, raw in enumerate(open(path, encoding="utf-8").read().split("\n"), 1):
            m = re.match(r'^(\w+)\s*=\s*"(.*)"\s*$', raw)
            if raw.strip() == "[[entry]]":
                cur = None
            if not m:
                continue
            k, v = m.group(1), m.group(2).replace('\\"', '"')
            if k == "id":
                cur = v
            if cur is None and k != "id":
                continue
            if k == "title" and cur:
                titles[cur] = v
            if k in ("title", "subtitle", "body", "field_note", "lore", "caption"):
                entries.append((rel(path), ln, k, v, cur))
    return titles, entries


def name_patterns(titles):
    """Multi-word archive titles and faction names whose exact case matters."""
    names = set()
    for t in titles.values():
        if len(t.split()) >= 2 and len(t) <= 40 and not re.search(r"[\"'()/:]", t) and t == t.title() or \
           (len(t.split()) >= 2 and re.fullmatch(r"[A-Z][A-Za-z\-]+( [A-Z][A-Za-z\-]+)+", t)):
            names.add(t)
    names = {re.sub(r"^The ", "", n) for n in names}
    names = {n for n in names if len(n.split()) >= 2}
    names |= {"Continental Reconstruction Authority", "Directorate of the Interior", "Left Behind", "Overseas Expedition"}
    # Small words inside a title ("of", "the") are allowed lower-case; an exact-case check on the whole name suffices.
    return sorted(names, key=lambda s: (-len(s), s))


# ---------------------------------------------------------------- checks
def check_text(text, ctx, names, allow_caps_style=False, in_draw=True):
    """Return a list of (category, detail) for one player-visible string."""
    flags = []
    t = text
    plain = re.sub(r"\{[^{}]*\}", "{}", t)  # placeholders collapsed
    # raw ids
    if ctx.get("is_cs") and ID_HOLE.search(re.sub(r"\{[^{}]*(?:Display|Title|Tidy)\([^{}]*\}", "{}", t)) and re.search(r"[A-Za-z]{3}", plain):
        flags.append(("raw_id", "interpolated id: " + ID_HOLE.search(re.sub(r"\{[^{}]*(?:Display|Title|Tidy)\([^{}]*\}", "{}", t)).group(0)))
    if ctx.get("is_cs"):
        scan = plain
    else:
        scan = t
    sn = SNAKE.search(scan)
    if sn and not (ctx.get("is_cs") and re.match(r"^[a-z][a-z0-9_]*$", t) and not ctx.get("drawn")):
        flags.append(("raw_id", "snake_case id: " + sn.group(0)))
    # dotted keys
    if DOTTED.match(t.strip()) and not FILE_EXT.search(t.strip()) and (ctx.get("drawn") or not ctx.get("is_cs")):
        flags.append(("dotted_key", "dotted key: " + t.strip()))
    for m in re.finditer(r"(?<![\w.])(?!www\.)[a-z]{1,4}[0-9]?\.[a-z]+(?:\.[a-z0-9_]+)+(?![\w])", scan):
        if not FILE_EXT.search(m.group(0)):
            flags.append(("dotted_key", "dotted key inside text: " + m.group(0)))
            break
    # brackets
    for m in BRACKET.finditer(plain):
        if KEY_LABEL.match(m.group(0)):
            continue
        flags.append(("placeholder_bracket", "bracket text: " + m.group(0)))
        break
    # dev wording
    dm = DEVWORD.search(plain)
    if dm:
        flags.append(("dev_wording", "dev word: " + dm.group(0)))
    # punctuation
    if DOUBLEDOT.search(plain):
        flags.append(("punctuation", "double period or ellipsis"))
    # programmer words (only in text with a space; one-word strings are labels)
    if " " in plain.strip():
        pm = PROGWORD.search(re.sub(r"\{[^{}]*\}", " ", t))
        if pm:
            flags.append(("programmer_word", "programmer word: " + pm.group(0)))
    # capitalisation of known names (skip ALL-CAPS in-universe text)
    if not allow_caps_style and not (t.upper() == t and has_letters(t)):
        low = t.lower()
        for nm in names:
            idx = low.find(nm.lower())
            while idx >= 0:
                found = t[idx:idx + len(nm)]
                before = t[idx - 1] if idx > 0 else " "
                after = t[idx + len(nm)] if idx + len(nm) < len(t) else " "
                if found != nm and not before.isalnum() and not after.isalnum():
                    # sentence-initial capital and Title-Case variants are fine only when exact; otherwise flag
                    flags.append(("capitalisation", f"'{found}' vs archive '{nm}'"))
                    break
                idx = low.find(nm.lower(), idx + 1)
            else:
                continue
            break
    return flags


# ---------------------------------------------------------------- scanning
def cs_files():
    seen = []
    for d in CS_DIRS:
        for dp, _, fns in os.walk(os.path.join(ROOT, d)):
            for fn in sorted(fns):
                if fn.endswith(".cs"):
                    seen.append(os.path.join(dp, fn))
    for f in CS_FILES:
        seen.append(os.path.join(ROOT, f))
    return sorted(set(seen))


# Lines where an id is turned into player text without going through an Archive title (seeded from the owner's report, then grown).
LEAK_LINES = [
    (re.compile(r'string\.Join\(", ", (NewTech|prerequisites|Prerequisites)\)'), "list of raw ids joined into text"),
    (re.compile(r'new\[\] \{ id, |new List<string> \{ \w+\.Id, '), "type id is the first info line"),
    (re.compile(r'\$"[^"]*\{(need|prereq|req)\}'), "raw prerequisite id in text"),
    (re.compile(r': m\.Id, m\.State|\? TitleOf\(dataDir, m\.Id\) : m\.Id'), "mission id used as the title fallback"),
    (re.compile(r"(?<!Attr)\.Replace\('_', ' '\)"), "ad-hoc id tidying (use CameoNames.Display)"),
    (re.compile(r"id\[\(u \+ 1\)\.\.\]"), "ad-hoc id tidying (use CameoNames.Display)"),
]


def scan_cs(names):
    flagged, debug = [], []
    total = 0
    for path in cs_files():
        r = rel(path)
        if os.path.basename(path) in SKIP_FILES or r.startswith(SKIP_DIRS):
            continue
        src = open(path, encoding="utf-8").read()
        lines = src.split("\n")
        is_dbg_file = os.path.basename(path) in DEBUG_FILES
        for ln0, L0 in enumerate(lines, 1):
            if L0.strip().startswith("//"):
                continue
            for rx, why in LEAK_LINES:
                if rx.search(L0):
                    item = (r, ln0, L0.strip(), [("raw_id", why)])
                    (debug if is_dbg_file else flagged).append(item)
                    break
        for ln, kind, text, interp in cs_literals(src):
            if not has_letters(text) or len(text) < 3:
                continue
            L = lines[ln - 1] if ln - 1 < len(lines) else ""
            stripped = L.strip()
            if stripped.startswith(("///", "[GeneratedRegex", "[InlineData")) or NONTEXT_CALL.search(L):
                continue
            if stripped.startswith("[") and stripped.endswith("]") and "Obsolete" in stripped:
                continue
            # A literal with no space and not drawn is an id/dictionary key/format string: not player text.
            drawn = bool(re.search(r"\.Draw\w*\(|Toast|Text\(|Label|Caption|Title|Hint|Tooltip|Tip\b|\b(?:Draw|Add)Line\(|Lines|Header|Subtitle|Notice|Message", L))
            if " " not in text.strip() and not drawn:
                continue
            if re.search(r"\bconst string\b", L) and DOTTED.match(text.strip()):
                continue   # a key constant; the Game.Tests text test checks what it resolves to
            if " " not in text.strip() and re.search(r'"' + re.escape(text) + r'"\s*(=>|\]|:|\))', L):
                continue   # the key side of a mapping, not drawn
            if " " not in text.strip() and re.search(r"sb\.Append(Line)?\(", L):
                continue   # file writers
            if re.search(r"[\\/]", text) and " " not in text:
                continue
            if re.match(r"^\s*(case|\[)?\s*\"[^\"]*\"\s*(:|=>)\s*$", L) and " " not in text and not drawn:
                continue
            ctx = {"is_cs": True, "drawn": drawn}
            fl = check_text(text, ctx, names)
            total += 1
            if not fl:
                continue
            item = (r, ln, text, fl)
            if is_dbg_file or DEBUG_LINE.search(L) or DEBUG_LINE.search(text):
                debug.append(item)
            else:
                flagged.append(item)
    return flagged, debug, total


def scan_locale(names):
    flagged, total = [], 0
    for d in sorted(os.listdir(os.path.join(ROOT, "data/missions"))):
        p = os.path.join(ROOT, "data/missions", d, "locale.toml")
        if not os.path.exists(p):
            continue
        in_voice = False
        for ln, raw in enumerate(open(p, encoding="utf-8").read().split("\n"), 1):
            if raw.strip().startswith("[voice]"):
                in_voice = True
            if in_voice or raw.strip().startswith("#"):
                continue
            m = re.match(r'^\s*"?([A-Za-z0-9_.]+)"?\s*=\s*"(.*)"\s*(#.*)?$', raw)
            if not m:
                continue
            total += 1
            fl = check_text(m.group(2).replace('\\"', '"'), {"is_cs": False}, names)
            if fl:
                flagged.append((rel(p), ln, m.group(2), fl))
    return flagged, total


def scan_archive(titles, entries, names):
    flagged, total = [], 0
    for path, ln, k, v, cid in entries:
        total += 1
        fl = check_text(v, {"is_cs": False}, names, allow_caps_style=(k in ("field_note", "subtitle")))
        if k == "title" and ("_" in v or DOTTED.match(v)):
            fl.append(("raw_id", "title contains underscore or looks like a key"))
        if fl:
            flagged.append((path, ln, f"[{cid}.{k}] {v}", fl))
    return flagged, total


def scan_briefings(names):
    flagged, total = [], 0
    for d in sorted(os.listdir(os.path.join(ROOT, "data/missions"))):
        p = os.path.join(ROOT, "data/missions", d, "briefing.md")
        if not os.path.exists(p):
            continue
        for ln, raw in enumerate(open(p, encoding="utf-8").read().split("\n"), 1):
            if not raw.strip():
                continue
            total += 1
            fl = check_text(raw, {"is_cs": False}, names, allow_caps_style=True)
            if fl:
                flagged.append((rel(p), ln, raw, fl))
    return flagged, total


def scan_data_ui(names):
    flagged, total = [], 0
    for fn in ("data/ui/tips.toml", "data/ui/credits.toml"):
        p = os.path.join(ROOT, fn)
        for ln, raw in enumerate(open(p, encoding="utf-8").read().split("\n"), 1):
            m = re.match(r'^\s*(text|name|credit|heading|title|subtitle|closing)\s*=\s*"(.*)"\s*$', raw)
            if not m:
                continue
            total += 1
            fl = check_text(m.group(2), {"is_cs": False}, names)
            fl = [f for f in fl if f[0] != "dev_wording" or "test" not in f[1].lower()]
            if fl:
                flagged.append((fn, ln, m.group(2), fl))
    return flagged, total


def run():
    titles, entries = read_archive()
    names = name_patterns(titles)
    groups = OrderedDict()
    cs, dbg, tcs = scan_cs(names)
    groups["C# UI strings (src/Game)"] = (cs, tcs)
    loc, tl = scan_locale(names)
    groups["Mission locale.toml values"] = (loc, tl)
    ar, ta = scan_archive(titles, entries, names)
    groups["Archive titles, bodies, field notes"] = (ar, ta)
    br, tb = scan_briefings(names)
    groups["Briefings (briefing.md)"] = (br, tb)
    du, td = scan_data_ui(names)
    groups["data/ui text (tips, credits)"] = (du, td)
    return groups, dbg


def counts(groups):
    c = OrderedDict((k, 0) for k in CATEGORIES)
    for _, (items, _) in groups.items():
        for _, _, _, fl in items:
            for cat in {f[0] for f in fl}:
                c[cat] += 1
    return c


def render(groups, dbg, heading):
    c = counts(groups)
    out = [f"## {heading}", ""]
    out.append("Strings scanned: " + ", ".join(f"{k} {t}" for k, (_, t) in groups.items()) + ".")
    out.append("")
    out.append("| Category | Flagged strings |")
    out.append("|---|---|")
    for k, desc in CATEGORIES.items():
        out.append(f"| {desc} | {c[k]} |")
    distinct = sum(len(items) for items, _ in groups.values())
    out.append(f"| **Distinct flagged strings** | **{distinct}** |")
    out.append("")
    for g, (items, _) in groups.items():
        out.append(f"### {g} ({len(items)})")
        out.append("")
        if not items:
            out.append("None.")
            out.append("")
            continue
        for path, ln, text, fl in items:
            shown = text if len(text) <= 110 else text[:107] + "..."
            cats = "; ".join(f"{a}: {b}" for a, b in fl)
            out.append(f"- `{path}:{ln}` {cats} -- `{shown.replace('`', chr(39))}`")
        out.append("")
    out.append(f"### Debug only ({len(dbg)})")
    out.append("")
    out.append("Strings in debug consoles, the script debug line and the Show diagnostics line. Not shown to players by default; not counted above.")
    out.append("")
    for path, ln, text, fl in dbg:
        shown = text if len(text) <= 90 else text[:87] + "..."
        out.append(f"- `{path}:{ln}` {'; '.join(b for _, b in fl)} -- `{shown.replace('`', chr(39))}`")
    out.append("")
    return "\n".join(out), c, distinct


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--write")
    ap.add_argument("--section", default="Before")
    ap.add_argument("--counts", action="store_true")
    ap.add_argument("--append", action="store_true")
    a = ap.parse_args()
    groups, dbg = run()
    text, c, distinct = render(groups, dbg, a.section)
    if a.counts:
        for k, v in c.items():
            print(f"{k}\t{v}")
        print(f"distinct\t{distinct}")
        return 0
    if a.write:
        mode = "a" if a.append else "w"
        with open(os.path.join(ROOT, a.write), mode, encoding="utf-8") as f:
            f.write(text + "\n")
    else:
        print(text)
    print(f"distinct flagged: {distinct}; debug only: {len(dbg)}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
