#!/usr/bin/env python3
"""Stage plugin DLLs (+ config .hjson) into the compose plugin folders.

The shell version this replaces used mkdir/rm/cp, a `for` loop, `[ -d ]`, `basename` and `ls | wc` —
none of which exist under cmd.exe. The rules are unchanged:

- each server gets its own empty plugins/ folder,
- every bundled plugin that is not ExamplePlugin stages into plugins/game, because they all reference
  OpenS4L.Server.Game,
- ExamplePlugin is deliberately left out: its ExamplePluginGameRule registers itself for Touchdown
  mode and cancels GameStateMachine's ScheduleTrigger hook without implementing a manual trigger, so
  every Touchdown room freezes at the post-loading countdown. Load it only to exercise the hook API.
"""

from __future__ import annotations

import shutil
import sys
from pathlib import Path

DOCKER = Path.cwd()
SERVERS = ('auth', 'chat', 'game', 'relay')
EXCLUDED = 'OpenS4L.Plugins.ExamplePlugin'
OUTPUT_CONFIGURATION = 'Release'
FRAMEWORK = 'net10.0'


def main() -> int:
    docker = Path.cwd()
    plugins_root = docker.parent / 'opens4l/src/plugins'
    if not plugins_root.is_dir():
        print(f'  no plugin sources at {plugins_root}', file=sys.stderr)
        return 1

    for server in SERVERS:
        folder = docker / 'plugins' / server
        folder.mkdir(parents=True, exist_ok=True)
        # Only build output is cleared. A `.hjson` in place is the operator's config, not output:
        # the old shell recipe deleted them and re-copied the build default, which silently turned
        # the tracked SoloMode config (Enabled: true) back off.
        for stale in folder.glob('*.dll'):
            stale.unlink()

    game = docker / 'plugins' / 'game'
    staged = []
    for project in sorted(plugins_root.glob('OpenS4L.Plugins.*')):
        if project.name == EXCLUDED:
            continue
        built = project / 'bin' / OUTPUT_CONFIGURATION / FRAMEWORK
        if not built.is_dir():
            print(f'  skipped {project.name}: no {OUTPUT_CONFIGURATION}/{FRAMEWORK} build')
            continue
        for pattern in ('*.dll', '*.hjson'):
            for file in sorted(built.glob(pattern)):
                target = game / file.name
                # A .hjson in the destination is the operator's config — the tracked one in this repo
                # has SoloMode Enabled: true while the build emits Enabled: false. Seed it, never
                # overwrite it: staging must not silently switch a plugin off.
                if pattern == '*.hjson' and target.exists():
                    print(f'  kept existing {file.name}')
                    continue
                shutil.copy2(file, target)
                staged.append(file.name)

    print(f'  staged {len(staged)} file(s) into plugins/game; auth/chat/relay are empty')
    return 0


if __name__ == '__main__':
    sys.exit(main())
