#!/usr/bin/env python3
"""Bring up the OpenS4L stack and open the admin dashboard."""

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
ADMIN_DIR = ROOT / "Tools" / "s4l-admin-console"
ADMIN_SERVER = ADMIN_DIR / "server" / "server.js"
ADMIN_URL = "http://127.0.0.1:8020"


def run(command: list[str], cwd: Path = ROOT) -> None:
    print(f"\n$ {' '.join(command)}", flush=True)
    subprocess.run(command, cwd=cwd, check=True)


def port_is_open(host: str, port: int) -> bool:
    with socket.socket() as sock:
        sock.settimeout(0.25)
        return sock.connect_ex((host, port)) == 0


def start_admin() -> None:
    if port_is_open("127.0.0.1", 8020):
        print(f"Admin dashboard already running at {ADMIN_URL}", flush=True)
        return

    node = shutil.which("node")
    if node is None:
        raise RuntimeError("Node.js is required to run the admin dashboard")

    log_path = Path(os.environ.get("OPENS4L_ADMIN_LOG", "/tmp/opens4l-admin-console.log"))
    log_file = log_path.open("ab")
    common_args = {
        "cwd": ADMIN_DIR,
        "stdin": subprocess.DEVNULL,
        "stdout": log_file,
        "stderr": subprocess.STDOUT,
    }
    if os.name == "posix":
        subprocess.Popen([node, str(ADMIN_SERVER)], start_new_session=True, **common_args)
    elif sys.platform == "win32":
        subprocess.Popen(
            [node, str(ADMIN_SERVER)],
            creationflags=subprocess.CREATE_NEW_PROCESS_GROUP | subprocess.DETACHED_PROCESS,
            **common_args,
        )
    else:
        subprocess.Popen([node, str(ADMIN_SERVER)], **common_args)
    log_file.close()

    for _ in range(30):
        if port_is_open("127.0.0.1", 8020):
            print(f"Admin dashboard running at {ADMIN_URL}", flush=True)
            return
        time.sleep(0.2)

    raise RuntimeError(f"Admin dashboard did not start; see {log_path}")


def main() -> int:
    try:
        run(["make", "-C", "Server/Docker", "bootstrap"])
        run(["make", "admin"])
        start_admin()
        opened = webbrowser.open(ADMIN_URL, new=2)
        if opened:
            print(f"Opened {ADMIN_URL} in the default browser", flush=True)
        else:
            print(f"Could not open a browser automatically; visit {ADMIN_URL}", flush=True)
        return 0
    except (subprocess.CalledProcessError, RuntimeError) as error:
        print(f"bootstrap failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
