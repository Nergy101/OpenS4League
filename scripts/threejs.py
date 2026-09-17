#!/usr/bin/env python3
"""Start a local Three.js viewer and open it in the default browser."""

from __future__ import annotations

import os
import shutil
import socket
import subprocess
import sys
import time
import webbrowser
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CLIENT = ROOT / "Client"
PORT = 8132
URLS = {
    "map-viewer": f"http://127.0.0.1:{PORT}/",
    "character-viewer": f"http://127.0.0.1:{PORT}/character.html",
}


def port_is_open() -> bool:
    with socket.socket() as sock:
        sock.settimeout(0.25)
        return sock.connect_ex(("127.0.0.1", PORT)) == 0


def list_viewers() -> None:
    print("Available Three.js viewers:")
    print("  map-viewer       Station-2 map and flying camera")
    print("  character-viewer Character and wardrobe viewer")
    print("\nUsage: make threejs map-viewer")


def main() -> int:
    viewer = sys.argv[1] if len(sys.argv) > 1 else ""
    if not viewer:
        list_viewers()
        return 0
    if viewer not in URLS:
        print(f"Unknown Three.js viewer: {viewer}\n", file=sys.stderr)
        list_viewers()
        return 2

    npm = shutil.which("npm") or shutil.which("npm.cmd")
    if npm is None:
        raise RuntimeError("npm is required to run the Three.js viewers")

    if not (CLIENT / "node_modules/three").exists():
        print("Installing Client dependencies...", flush=True)
        subprocess.run([npm, "ci"], cwd=CLIENT, check=True)

    if not port_is_open():
        log_path = Path(os.environ.get("OPENS4L_THREEJS_LOG", "/tmp/opens4l-threejs.log"))
        log_file = log_path.open("ab")
        common_args = {
            "cwd": CLIENT,
            "stdin": subprocess.DEVNULL,
            "stdout": log_file,
            "stderr": subprocess.STDOUT,
        }
        if os.name == "posix":
            subprocess.Popen([npm, "start"], start_new_session=True, **common_args)
        elif sys.platform == "win32":
            subprocess.Popen(
                [npm, "start"],
                creationflags=subprocess.CREATE_NEW_PROCESS_GROUP | subprocess.DETACHED_PROCESS,
                **common_args,
            )
        else:
            subprocess.Popen([npm, "start"], **common_args)
        log_file.close()
        for _ in range(30):
            if port_is_open():
                break
            time.sleep(0.2)

    if not port_is_open():
        raise RuntimeError("Three.js viewer did not start; see /tmp/opens4l-threejs.log")

    url = URLS[viewer]
    opened = webbrowser.open(url, new=2)
    print(f"Three.js viewer available at {url}", flush=True)
    if not opened:
        print("Could not open a browser automatically; use the URL above", flush=True)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (subprocess.CalledProcessError, RuntimeError) as error:
        print(f"threejs failed: {error}", file=sys.stderr)
        raise SystemExit(1)
