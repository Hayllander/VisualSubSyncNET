"""Read-only source inventory; generated candidates are navigation aids, not proof of behavioral parity."""
from pathlib import Path
import argparse
import hashlib
import json
import re


def normalized_delphi(text):
    # Keep quoted strings and compiler directives; discard ordinary comments and whitespace.
    pattern = r"'(?:''|[^'])*'|\{\$[^}]*\}|\(\*\$[\s\S]*?\*\)|//[^\n]*|\{[^}]*\}|\(\*[\s\S]*?\*\)|\s+|."
    result = []
    for match in re.finditer(pattern, text):
        token = match.group()
        if token.startswith("'"):
            result.append(token)
        elif token.startswith(("{$", "(*$")):
            result.append(token.lower())
        elif not token.isspace() and not token.startswith(("//", "{", "(*")):
            result.append(token.lower())
    return "".join(result)


def routines(path):
    text = path.read_text(encoding="utf-8-sig") if path.read_bytes().startswith(b"\xef\xbb\xbf") else path.read_text(encoding="cp1252")
    matches = list(re.finditer(r"(?mi)^(?:procedure|function|constructor|destructor)\s+([\w.]+)", text))
    counts = {}
    result = []
    for i, match in enumerate(matches):
        name = match[1]
        counts[name.lower()] = counts.get(name.lower(), 0) + 1
        body = text[match.start():matches[i+1].start() if i+1 < len(matches) else len(text)]
        result.append(dict(name=name, ordinal=counts[name.lower()], line=text.count("\n", 0, match.start())+1,
                           hash=hashlib.sha256(normalized_delphi(body).encode()).hexdigest()))
    return result


def run(project, original, enhanced):
    output = project / "docs"
    output.mkdir(exist_ok=True)
    index = {}
    for path in (project / "src").rglob("*"):
        if path.suffix not in (".cs", ".xaml") or {"bin", "obj"}.intersection(path.parts):
            continue
        for name in set(re.findall(r"\b\w+\b", path.read_text(encoding="utf-8-sig"))):
            index.setdefault(name.lower(), set()).add(str(path.relative_to(project)))
    entries, comparison = [], []
    for path in sorted(original.glob("*.pas")):
        source = routines(path)
        other = routines(enhanced / path.name) if (enhanced / path.name).exists() else []
        original_map = {(r['name'].lower(), r['ordinal']): r for r in source}
        enhanced_map = {(r['name'].lower(), r['ordinal']): r for r in other}
        for key in sorted(original_map.keys() | enhanced_map.keys()):
            a, b = original_map.get(key), enhanced_map.get(key)
            status = "enhanced-only" if a is None else "original-only" if b is None else "same-normalized" if a['hash'] == b['hash'] else "changed"
            comparison.append(dict(unit=path.name, status=status, original=a, enhanced=b))
        for routine in source:
            name = routine['name'].split('.')[-1]
            names = [name.lower()]
            if name.startswith("Action") and name.endswith("Execute"):
                names.append(name[6:-7].lower())
            candidates = sorted(set().union(*(index.get(n, set()) for n in names)))
            entries.append(dict(unit=path.name, routine=routine, dotnet_text_candidates=candidates))
    extras = [dict(unit=p.name, routines=routines(p)) for p in sorted(enhanced.glob("*.pas")) if not (original / p.name).exists()]
    resources = {}
    for folder in ("jsplugin", "web", "help"):
        a, b = original / "Release" / folder, project / "src" / "VisualSubSync.App" / folder
        files = []
        for path in sorted(a.rglob("*")):
            if path.is_file():
                target = b / path.relative_to(a)
                files.append(dict(file=str(path.relative_to(a)), status="missing" if not target.exists() else "identical" if path.read_bytes() == target.read_bytes() else "different"))
        resources[folder] = files
    data = dict(original=str(original), enhanced_source=str(enhanced), entries=entries,
                delphi_comparison=comparison, enhanced_new_units=extras, original_resources=resources,
                limitation="Text candidates and normalized diffs are not semantic verification. Overloads are paired by name and occurrence. Enhanced executable 1.2.29 differs from source 1.2.33.0.")
    destination = output / "RASTREABILIDADE-PARIDADE-2026-10-21.json"
    destination.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(dict(routines=len(entries), comparison={s:sum(r['status']==s for r in comparison) for s in sorted({r['status'] for r in comparison})},
                          enhanced_new_units=len(extras), output=str(destination)), ensure_ascii=False))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--original", type=Path, default=Path(r"C:\Projetos\VisualSubSync"))
    parser.add_argument("--enhanced", type=Path, default=Path(r"C:\Projetos\VisualSubSyncEnh\fontes"))
    args = parser.parse_args()
    run(args.project, args.original, args.enhanced)
