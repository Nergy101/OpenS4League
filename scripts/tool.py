#!/usr/bin/env python3
"""Build and launch one of the OpenS4L tools."""

from __future__ import annotations

import os
import socket
import subprocess
import sys
import tempfile
import time
import webbrowser
from pathlib import Path

import hostapp

ROOT = Path(__file__).resolve().parent.parent
TOOLS = ROOT / "Tools"
ADMIN_URL = "http://127.0.0.1:8020"

TOOL_PROJECTS = {
    "s4l-resource-tool": TOOLS / "s4l-resource-tool/src/S4LResourceTool.App/S4LResourceTool.App.csproj",
    "s4l-character-viewer": TOOLS / "s4l-character-viewer/S4LCharacterViewer.csproj",
    "s4l-map-editor": TOOLS / "s4l-map-editor/S4LMapEditor.csproj",
    "s4l-animation-creator": TOOLS / "s4l-animation-creator/S4LAnimationCreator.csproj",
    "s4l-item-editor": TOOLS / "s4l-item-editor/S4LItemEditor.csproj",
    "s4l-client-configurator": TOOLS / "s4l-client-configurator/S4LClientConfigurator.csproj",
    "s4l-client-mod-packer": TOOLS / "s4l-client-mod-packer/S4LClientModPacker.csproj",
    "s4l-server-config-tool": TOOLS / "s4l-server-config-tool/S4LServerConfigTool.csproj",
    "s4l-legacy-migration": TOOLS / "s4l-legacy-migration/S4LLegacyMigration.csproj",
    "s4l-resource-diff": TOOLS / "s4l-resource-diff/S4LResourceDiff.csproj",
    "s4l-localisation-editor": TOOLS / "s4l-localisation-editor/S4LLocalisationEditor.csproj",
}


def run(command: list[str], cwd: Path = ROOT) -> None:
    print(f"\n$ {' '.join(command)}", flush=True)
    subprocess.run(command, cwd=cwd, check=True)


def port_is_open(port: int) -> bool:
    with socket.socket() as sock:
        sock.settimeout(0.25)
        return sock.connect_ex(("127.0.0.1", port)) == 0


def start_admin() -> None:
    admin_dir = TOOLS / "s4l-admin-console"
    pnpm = hostapp.command("pnpm") or ["pnpm"]
    run([*pnpm, "install"], admin_dir / "web")
    run([*pnpm, "run", "build"], admin_dir / "web")
    if not port_is_open(8020):
        node = hostapp.command("node")
        if node is None:
            raise RuntimeError("Node.js is required to run s4l-admin-console")
        default_log = Path(tempfile.gettempdir()) / "opens4l-admin-console.log"
        log_path = Path(os.environ.get("OPENS4L_ADMIN_LOG", str(default_log)))
        log_file = log_path.open("ab")
        subprocess.Popen(
            [*node, str(admin_dir / "server/server.js")],
            cwd=admin_dir,
            stdin=subprocess.DEVNULL,
            stdout=log_file,
            stderr=subprocess.STDOUT,
            start_new_session=(os.name == "posix"),
        )
        log_file.close()
        for _ in range(30):
            if port_is_open(8020):
                break
            time.sleep(0.2)
    if not port_is_open(8020):
        raise RuntimeError("Admin dashboard did not start")
    webbrowser.open(ADMIN_URL, new=2)
    print(f"Admin dashboard available at {ADMIN_URL}", flush=True)


def list_tools() -> None:
    print("Available tools:")
    for name in (*TOOL_PROJECTS, "s4l-admin-console"):
        print(f"  {name}")
    print("\nUsage: make tool <toolname>")


def main() -> int:
    name = sys.argv[1] if len(sys.argv) > 1 else ""
    if not name:
        list_tools()
        return 0
    if name == "s4l-admin-console":
        start_admin()
        return 0
    project = TOOL_PROJECTS.get(name)
    if project is None:
        print(f"Unknown tool: {name}\n", file=sys.stderr)
        list_tools()
        return 2
    if not project.exists():
        print(f"Tool project not found: {project}", file=sys.stderr)
        return 1
    run(["dotnet", "build", "-c", "Release", str(project)])
    run(["dotnet", "run", "-c", "Release", "--no-build", "--project", str(project)])
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (subprocess.CalledProcessError, RuntimeError) as error:
        print(f"tool failed: {error}", file=sys.stderr)
        raise SystemExit(1)
