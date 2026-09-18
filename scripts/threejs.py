#!/usr/bin/env python3
"""Start a local Three.js viewer and open it in the default browser."""

from __future__ import annotations

import json
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
MAPS = CLIENT / "Models" / "Maps"
# The index the converter writes next to the maps it produces is the single
# source of truth for which maps exist and which files belong to each one.
MAP_INDEX = MAPS / "index.json"
MAP_INDEX_FORMAT = "s4-maps-index"
PORT = 8132
VIEWERS = ("map-viewer", "character-viewer", "convert-assets")
CHARACTER_URL = f"http://127.0.0.1:{PORT}/character.html"
CONVERTER = ROOT / "Tools" / "s4l-threejs-converter"
# One recipe per convertable map; the recipe names the map and its client configuration.
RECIPES = CONVERTER / "maps"
# The client ZIP is user-supplied and never committed; there is deliberately no default.
ARCHIVE = Path(os.environ["S4_CLIENT_ZIP"]).expanduser() if os.environ.get("S4_CLIENT_ZIP") else None


def recipes() -> list[dict]:
    return [
        {
            "id": path.stem,
            "recipe": path,
            "name": json.loads(path.read_text(encoding="utf-8"))["name"],
        }
        for path in sorted(RECIPES.glob("*.json"))
    ]


def convert_assets(selection: str) -> int:
    if ARCHIVE is None:
        raise RuntimeError(
            "Set S4_CLIENT_ZIP=/path/to/your client ZIP (user-supplied client data is never committed)."
        )
    if not ARCHIVE.is_file():
        raise RuntimeError(
            f"Client ZIP not found: {ARCHIVE}. Set S4_CLIENT_ZIP=/path/to/client.zip."
        )
    project = CONVERTER / "S4League.ThreeJs.Converter.csproj"
    if not project.is_file():
        raise RuntimeError(f"Three.js converter not found: {project}")

    selected = recipes()
    if selection:
        wanted = selection.lower()
        selected = [m for m in selected if m["id"] == wanted or m["name"].lower() == wanted]
        if not selected:
            raise RuntimeError(
                f"Unknown map: {selection}. Convertable maps: "
                + ", ".join(f"{m['id']} ({m['name']})" for m in recipes())
            )
    if not selected:
        raise RuntimeError(f"No map recipes found in {RECIPES}")

    run(["dotnet", "build", "-c", "Release", str(project)])
    failures = []
    for index, map_recipe in enumerate(selected, start=1):
        output = MAPS / map_recipe["name"]
        print(f"\n[{index}/{len(selected)}] {map_recipe['id']} -> {output.relative_to(ROOT)}", flush=True)
        try:
            run([
                "dotnet", "run", "-c", "Release", "--no-build", "--project", str(project),
                "--", str(ARCHIVE), str(output.relative_to(ROOT)), "--map", str(map_recipe["recipe"].relative_to(ROOT)),
            ])
        except subprocess.CalledProcessError:
            # One map's failure must not stop a roster-sized batch; the reason is in the output above.
            failures.append(map_recipe["id"])
            print(f"FAILED: {map_recipe['id']}", file=sys.stderr, flush=True)
    if failures:
        print(f"Converted {len(selected) - len(failures)} of {len(selected)} maps. Failed: {', '.join(failures)}", file=sys.stderr)
        return 1
    if selection:
        print(f"Converted map {selection}.", flush=True)
        return 0
    run([
        "dotnet", "run", "-c", "Release", "--no-build", "--project", str(project),
        "--", str(ARCHIVE), "Client/Models/Characters/BasicFemale",
        "--character", "Tools/s4l-threejs-converter/characters/female-basic.json",
    ])
    print("Converted map and character assets.", flush=True)
    return 0


def run(command: list[str], cwd: Path = ROOT) -> None:
    print(f"\n$ {' '.join(command)}", flush=True)
    subprocess.run(command, cwd=cwd, check=True)


def port_is_open() -> bool:
    with socket.socket() as sock:
        sock.settimeout(0.25)
        return sock.connect_ex(("127.0.0.1", PORT)) == 0


def read_maps() -> list[dict]:
    if not MAP_INDEX.is_file():
        return []
    index = json.loads(MAP_INDEX.read_text(encoding="utf-8"))
    if index.get("format") != MAP_INDEX_FORMAT:
        raise RuntimeError(f"Unsupported map index: {MAP_INDEX}")
    return index.get("maps", [])


def describe(entry: dict) -> str:
    directory = MAPS / entry["directory"]
    manifest_path = directory / entry["manifest"]
    if not manifest_path.is_file():
        raise RuntimeError(f"{entry['id']}: missing {manifest_path}")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    totals = manifest.get("totals", {})
    return (
        f"  {entry['id']:<14} {manifest.get('name', entry['id']):<12}"
        f" {totals.get('scenes', 0)} SCNs · {totals.get('models', 0)} meshes ·"
        f" {totals.get('triangles', 0):,} triangles"
    )


def list_maps() -> None:
    maps = read_maps()
    if not maps:
        print("No converted maps yet. Convert one first: make threejs convert-assets")
        return
    print("Available maps:")
    for entry in maps:
        print(describe(entry))
    print("\nUsage: make threejs map-viewer <map>")


def find_map(requested: str) -> dict | None:
    wanted = requested.lower()
    return next(
        (
            entry
            for entry in read_maps()
            if entry["id"].lower() == wanted or entry.get("directory", "").lower() == wanted
        ),
        None,
    )


def list_viewers() -> None:
    print("Available Three.js viewers:")
    print("  map-viewer [<map>]      Converted map with a flying camera; lists the maps when no map is given")
    print("  character-viewer        Character and wardrobe viewer")
    print("  convert-assets [<map>]  Convert the map recipes and BasicFemale (needs S4_CLIENT_ZIP)")
    print("\nUsage: make threejs map-viewer [station-2]")


def open_viewer(url: str) -> None:
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

    opened = webbrowser.open(url, new=2)
    print(f"Three.js viewer available at {url}", flush=True)
    if not opened:
        print("Could not open a browser automatically; use the URL above", flush=True)


def main() -> int:
    viewer = sys.argv[1] if len(sys.argv) > 1 else ""
    argument = sys.argv[2] if len(sys.argv) > 2 else ""
    if not viewer:
        list_viewers()
        return 0
    if viewer == "convert-assets":
        return convert_assets(argument)
    if viewer not in VIEWERS:
        print(f"Unknown Three.js viewer: {viewer}\n", file=sys.stderr)
        list_viewers()
        return 2

    if viewer == "map-viewer":
        if not argument:
            list_maps()
            return 0
        entry = find_map(argument)
        if entry is None:
            print(f"Unknown map: {argument}\n", file=sys.stderr)
            list_maps()
            return 2
        open_viewer(f"http://127.0.0.1:{PORT}/?map={entry['id']}")
        return 0

    open_viewer(CHARACTER_URL)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (subprocess.CalledProcessError, RuntimeError) as error:
        print(f"threejs failed: {error}", file=sys.stderr)
        raise SystemExit(1)
