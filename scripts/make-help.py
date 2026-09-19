#!/usr/bin/env python3
"""Print a Makefile's target list, so `make help` works on Windows too.

The recipes it replaces were `grep -hE '...' | awk '...'`: grep and awk do not exist under cmd.exe,
which is the shell GNU make uses on Windows when no sh.exe is on PATH. Parsing in Python keeps the
listing identical on every platform, including the alignment.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

makefile = Path(sys.argv[1] if len(sys.argv) > 1 else 'Makefile')
if not makefile.is_file():
    raise SystemExit(f'No such Makefile: {makefile}')

# `target: ... ## Description` — the convention every Makefile in this repo follows.
pattern = re.compile(r'^([A-Za-z0-9_-]+):.*?##\s*(.*)$')
entries = []
for line in makefile.read_text(encoding='utf-8').splitlines():
    match = pattern.match(line)
    if match:
        entries.append((match.group(1), match.group(2).strip()))

if not entries:
    print(f'{makefile}: no documented targets')
    raise SystemExit(0)

width = max(len(name) for name, _ in entries)
for name, description in entries:
    print(f'  {name.ljust(width)}  {description}')
