#!/usr/bin/env python3
"""Recursively delete the given paths — the portable stand-in for `rm -rf`.

Globs are expanded here rather than by the shell: cmd.exe does not expand them, and the Makefiles
that clean build output must behave the same on Windows as they do on macOS. Missing paths are not
an error, which is what `|| true` was papering over before.
"""

from __future__ import annotations

import glob
import shutil
import sys
from pathlib import Path

removed = 0
for pattern in sys.argv[1:]:
    matches = glob.glob(pattern)
    for match in matches:
        target = Path(match)
        try:
            if target.is_dir() and not target.is_symlink():
                shutil.rmtree(target)
            else:
                target.unlink()
        except OSError as error:
            # A file held open by a running process must not turn `make clean` into a wall of text.
            print(f'  could not remove {target}: {error}', file=sys.stderr)
            continue
        print(f'  removed {target}')
        removed += 1

print(f'  {removed} path(s) removed')
