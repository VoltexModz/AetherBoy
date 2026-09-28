#!/usr/bin/env python3
"""Point out common UI-copy patterns for human review; never rewrite text."""

from __future__ import annotations

import argparse
import re
import sys
from html import unescape
from pathlib import Path


RULES = (
    (
        "Werbefloskel",
        re.compile(
            r"\b(?:nahtlos|mühelos|revolutionär|bahnbrechend|"
            r"seamlessly|effortlessly|game[ -]changing|next[ -]level)\b",
            re.IGNORECASE,
        ),
    ),
    (
        "interner Begriff im UI-Text",
        re.compile(r"\b(?:Browserfrei|ICE-Suche|DEVELOPMENT|GEN3 DEV)\b", re.IGNORECASE),
    ),
    (
        "Schrägstrichliste",
        re.compile(r"(?:[A-Za-zÄÖÜäöüß]{3,}\s*/\s*){2,}[A-Za-zÄÖÜäöüß]{3,}"),
    ),
    (
        "Slogan aus Satzfragmenten",
        re.compile(r"\b(?:Ein Raum\. Zwei Spieler\.|One room\. Two players\.)", re.IGNORECASE),
    ),
)


def review(name: str, content: str) -> int:
    findings = 0
    for number, line in enumerate(content.splitlines(), 1):
        stripped = line.lstrip()
        if stripped.startswith(("//", "#", "/*", "*")):
            continue
        if re.search(r"<(?:p|h[1-6]|summary|label|button|footer|div)\b", line):
            visible = unescape(re.sub(r"<[^>]*>", " ", line))
        else:
            visible = " ".join(re.findall(r'"(?:\\.|[^"\\])*"', line))
        if not visible:
            continue
        labels = [label for label, pattern in RULES if pattern.search(visible)]
        if visible.count(" · ") >= 2 and "SetWindowTitle" not in line:
            labels.append("mehrere Trenner statt Satz")
        if not labels:
            continue
        findings += 1
        excerpt = stripped[:140] + ("…" if len(stripped) > 140 else "")
        print(f"{name}:{number}: {', '.join(labels)}: {excerpt}")
    return findings


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("files", nargs="+", help="UI-Quelldateien oder - für Standardeingabe")
    parser.add_argument("--strict", action="store_true", help="Bei Treffern mit Code 1 beenden")
    args = parser.parse_args()
    total = 0
    for filename in args.files:
        if filename == "-":
            total += review("<stdin>", sys.stdin.read())
            continue
        path = Path(filename)
        try:
            total += review(str(path), path.read_text(encoding="utf-8"))
        except (OSError, UnicodeError) as error:
            parser.error(f"{path}: {error}")
    print(f"{total} Prüfpunkte. Alle sichtbaren Texte zusätzlich selbst lesen.")
    return 1 if args.strict and total else 0


if __name__ == "__main__":
    raise SystemExit(main())
