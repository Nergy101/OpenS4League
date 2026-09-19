#!/usr/bin/env python3
"""Launch host applications (node, npm, pnpm) the same way on macOS and Windows.

Node itself is a real executable, but npm and pnpm are `.cmd` shims on Windows. CreateProcess cannot
start a `.cmd` directly, so those have to go through `cmd /c`; on macOS and Linux the resolved path is
run as-is. Use `command('npm')` and splat the result into the argv list.
"""

from __future__ import annotations

import os
import shutil


def find(name: str) -> str | None:
    """The full path of a host application, trying the Windows shim names too."""
    return shutil.which(name) or shutil.which(f'{name}.cmd') or shutil.which(f'{name}.exe')


def command(name: str) -> list[str] | None:
    """The argv prefix that runs `name`, or None when it is not installed."""
    path = find(name)
    if path is None:
        return None
    if os.name == 'nt' and path.lower().endswith(('.cmd', '.bat')):
        return ['cmd', '/c', path]
    return [path]
