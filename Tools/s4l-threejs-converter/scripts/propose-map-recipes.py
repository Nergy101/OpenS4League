#!/usr/bin/env python3
"""Propose one map recipe per unique map in the client's own roster.

The client ships the authoritative map list in `xml/map.x7` (per-map bginfo configuration,
player limit, region flags) and the English names in
`language/xml/gameinfo_string_table.x7` (`MAPNAME_*`). This script picks one configuration
per unique map name and writes a recipe per map, so the viewer's map set is reproducible
from a client build instead of being typed by hand.

Selection: the region-enabled entry, then the one with the most players (the full map
rather than a 1-player variant), then the one whose configuration carries no mode/variant
token, then the lowest roster id. Non-map names (dev `Test`, `Random`, single-player
`Licence`/`Tutorial`/`Training Center`) are skipped.

Usage:
  scripts/propose-map-recipes.py --archive "$S4_CLIENT_ZIP"            # print the selection
  scripts/propose-map-recipes.py --archive "$S4_CLIENT_ZIP" --write    # write maps/*.json
"""
from __future__ import annotations

import argparse
import json
import re
import unicodedata
import xml.etree.ElementTree as ET
import zipfile
from collections import defaultdict
from pathlib import Path

RECIPES = Path(__file__).resolve().parent.parent / "maps"
SKIP_NAMES = {"Test", "test", "Random", "Licence", "Training Center", "Tutorial"}
VARIANT_TOKENS = (
    "ms_", "btc_", "captain", "lc_", "mh_", "seize", "tuto", "acade", "left4dead", "kstest",
    "new_deathmatch", "random", "photozone", "weapon_test", "pve",
    "_death", "_pvp", "_ffa", "_sl", "_ct", "_surv", "_test", "_d", "_t",
)
REGION_RANK = {"on": 0, "new": 1, "off": 2, "dev": 3}


def slug(text: str) -> str:
    text = unicodedata.normalize("NFKD", text).encode("ascii", "ignore").decode()
    return re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")


def bundle(text: str) -> str:
    return re.sub(r"[^a-z0-9]+", "", text.lower())


def map_names(archive: zipfile.ZipFile) -> dict[str, str]:
    strings = archive.read("Game/language/xml/gameinfo_string_table.x7")
    names = {}
    for match in re.finditer(rb"<string\b([^>]*)/>", strings):
        attributes = dict(re.findall(rb'(\w+)="([^"]*)"', match.group(1)))
        key = attributes.get(b"key", b"").decode("ascii", "replace")
        if key.startswith("MAPNAME_"):
            names[key] = attributes.get(b"eng", b"").decode("utf-8", "replace").strip()
    return names


def selection(archive: zipfile.ZipFile) -> dict[str, dict]:
    names = map_names(archive)
    shipped = {
        entry.rsplit("/", 1)[-1].lower()
        for entry in archive.namelist()
        if entry.lower().startswith("game/resources/mapinfo/bginfo")
    }
    groups: dict[str, list[dict]] = defaultdict(list)
    for element in ET.fromstring(archive.read("Game/xml/map.x7")).findall("map"):
        base = element.find("base")
        resource = element.find("resourse")
        if base is None or resource is None:
            continue
        switch = element.find("switch")
        config = (resource.get("bginfo_path") or "").rsplit("/", 1)[-1].lower()
        name = names.get(base.get("map_name_key") or "", "")
        if config not in shipped or not name or name in SKIP_NAMES:
            continue
        stem = config[len("bginfo-"):-len(".ini")]
        groups[name].append({
            "roster_id": int(element.get("id") or 0),
            "config": "resources/mapinfo/" + config,
            "region": (switch.get("eu") or "") if switch is not None else "",
            "players": int(base.get("limit_player") or 0),
            "variant_tokens": sum(token in stem for token in VARIANT_TOKENS),
        })
    return {
        name: {
            "name": name,
            "bundle": bundle(name),
            "config": sorted(entries, key=lambda e: (
                REGION_RANK.get(e["region"], 9), e["variant_tokens"], -e["players"], e["roster_id"]))[0]["config"],
            "roster_ids": sorted(entry["roster_id"] for entry in entries),
        }
        for name, entries in groups.items()
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", required=True, help="Season-8 client ZIP (user-supplied, never committed)")
    parser.add_argument("--write", action="store_true", help=f"write {RECIPES}/*.json instead of printing")
    arguments = parser.parse_args()

    with zipfile.ZipFile(arguments.archive) as archive:
        chosen = selection(archive)

    for name, recipe in sorted(chosen.items()):
        identifier = slug(name)
        if arguments.write:
            (RECIPES / f"{identifier}.json").write_text(json.dumps(
                {"name": recipe["name"], "bundle": recipe["bundle"], "config": recipe["config"]},
                indent=2) + "\n", encoding="utf-8")
        else:
            print(f"{identifier:<14} {name:<14} {recipe['bundle']:<12} {recipe['config']}")
    if arguments.write:
        print(f"Wrote {len(chosen)} recipes to {RECIPES}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
