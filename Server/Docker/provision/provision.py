#!/usr/bin/env python3
"""Provision the OpenS4L stack: create the admin account and load the free shop (PostgreSQL).

Runs as a one-shot Docker container. Waits for the Game server to have applied its EF Core
migrations (which create the DB tables), then:
  1. creates the admin account (SecurityLevel 3) if it doesn't exist,
  2. regenerates the "unlock everything" shop with correct client tabs and loads it.

Idempotent: safe to re-run on an already-provisioned stack.

Env:
  DB_HOST           PostgreSQL hostname on the compose network (default: postgres)
  DB_ROOT_PASSWORD  postgres superuser password (default: 1234)
  ADMIN_USER        admin username, stored lowercase (default: admin)
  ADMIN_PASSWORD    admin password (default: admin)
  ITEM_X7           path to the server's item.x7 (default: /data/xml/item.x7)
  BOT_COUNT         number of regular bot accounts to create (default: 0 = none)
  BOT_PREFIX        bot account username prefix (default: bot) -> bot0..botN-1
  BOT_PASSWORD      shared bot password (default: admin)
  ONLY_BOTS         when truthy, only create bot accounts (skip admin/shop/channels)
"""
import base64
import hashlib
import os
import time
import xml.etree.ElementTree as ET

import psycopg2

DB_HOST = os.getenv("DB_HOST", "postgres")
ROOT_PW = os.getenv("DB_ROOT_PASSWORD", "1234")
ADMIN_USER = os.getenv("ADMIN_USER", "admin").lower()
ADMIN_PASS = os.getenv("ADMIN_PASSWORD", "admin")
ITEM_X7 = os.getenv("ITEM_X7", "/data/xml/item.x7")
SCHEMA_TIMEOUT = int(os.getenv("SCHEMA_TIMEOUT", "180"))
BOT_COUNT = int(os.getenv("BOT_COUNT", "0"))
BOT_PREFIX = os.getenv("BOT_PREFIX", "bot")
BOT_PASSWORD = os.getenv("BOT_PASSWORD", "admin")
ONLY_BOTS = os.getenv("ONLY_BOTS", "0").lower() in ("1", "true", "yes")

# Matching OpenS4L.Common/Cryptography/PasswordHasher.cs
PBKDF2_ITERATIONS = 24000
PBKDF2_DKLEN = 24
SECURITY_LEVEL_ADMIN = 3


def connect(db=None):
    return psycopg2.connect(
        host=DB_HOST, port=5432, user="postgres", password=ROOT_PW,
        dbname=db, connect_timeout=5,
    )


def table_exists(cur, name):
    cur.execute(
        "SELECT 1 FROM information_schema.tables "
        "WHERE table_schema='public' AND table_name=%s", (name,))
    return cur.fetchone() is not None


def wait_for_schema():
    deadline = time.time() + SCHEMA_TIMEOUT
    while time.time() < deadline:
        try:
            with connect("game") as c, c.cursor() as cur:
                has_shop = table_exists(cur, "shop_items")
            with connect("auth") as c, c.cursor() as cur:
                has_accounts = table_exists(cur, "accounts")
            if has_shop and has_accounts:
                print("[provision] schema ready (shop_items + accounts exist)")
                return
        except Exception:
            pass
        print("[provision] waiting for DB schema (game server migrations)...")
        time.sleep(3)
    raise SystemExit(
        f"[provision] timed out after {SCHEMA_TIMEOUT}s waiting for schema. "
        "Is PostgreSQL up and has the game server started once?"
    )


def ensure_admin():
    with connect("auth") as conn, conn.cursor() as cur:
        cur.execute('SELECT COUNT(*) FROM "accounts" WHERE "Username"=%s', (ADMIN_USER,))
        if cur.fetchone()[0] > 0:
            print(f"[provision] admin account '{ADMIN_USER}' already exists (skipped)")
            return
        salt = os.urandom(PBKDF2_DKLEN)
        key = hashlib.pbkdf2_hmac("sha1", ADMIN_PASS.encode(), salt,
                                  PBKDF2_ITERATIONS, dklen=PBKDF2_DKLEN)
        cur.execute(
            'INSERT INTO "accounts" ("Username", "Nickname", "Password", "Salt", "SecurityLevel") '
            "VALUES (%s, NULL, %s, %s, %s)",
            (ADMIN_USER,
             base64.b64encode(key).decode(),
             base64.b64encode(salt).decode(),
             SECURITY_LEVEL_ADMIN),
        )
        print(f"[provision] created admin account '{ADMIN_USER}' (SecurityLevel 3)")


def ensure_channels():
    """Seed the game server's channel list (the client shows a Server, then the channels
    you can enter from the `channels` table in the game DB). Mirrors the EU v1267 client's
    `_eu_channel_setting.x7` layout (ids 1-11). The Game server's ChannelService loads this
    table at startup; with it empty the client sees a Server but no channel to enter (and so
    no room list / create-room screen). Color is an 8-digit ARGB hex string (varchar(8)),
    parsed by the server via Color.FromArgb(int.Parse(hex)).
    """
    # (id, name, description, rank, color_argb, player_limit, min_level, max_level)
    CHANNELS = [
        (1,  "Beginner 1", "ROOKIE",         "FREE", "FF297FFF", 400,  0,  20),
        (2,  "Beginner 2", "ROOKIE",         "FREE", "FF297FFF", 400,  0,  20),
        (3,  "Amateur 1",  "Super Rookie",   "FREE", "FF9B59FF", 600, 15,  35),
        (4,  "Amateur 2",  "Super Rookie",   "FREE", "FF9B59FF", 600, 15,  35),
        (5,  "Pro 1",      "Pro",            "FREE", "FF00A2FF", 600, 30,  55),
        (6,  "Pro 2",      "Pro",            "FREE", "FF00A2FF", 600, 30,  55),
        (7,  "Elite 1",    "Elite",          "FREE", "FFFF7F00", 600, 50,  80),
        (8,  "Elite 2",    "Elite",          "FREE", "FFFF7F00", 600, 50,  80),
        (9,  "Free 1",     "FREE",           "FREE", "FF00FF7F", 600,  0, 999),
        (10, "Free 2",     "Free Event",     "FREE", "FF00FF7F", 600,  8,  80),
        (11, "Clan",       "CLUB",           "FREE", "FF00FF7F", 300,  0, 999),
    ]
    with connect("game") as conn, conn.cursor() as cur:
        cur.execute('SELECT COUNT(*) FROM "channels"')
        count = cur.fetchone()[0]
        if count > 0:
            print(f"[provision] channels: {count} already present (skipped)")
            return
        for (cid, name, desc, rank, color, limit, mn, mx) in CHANNELS:
            cur.execute(
                'INSERT INTO "channels" ("Id", "Name", "Description", "Color", '
                '"PlayerLimit", "MinLevel", "MaxLevel") VALUES (%s,%s,%s,%s,%s,%s,%s)',
                (cid, name, desc, color, limit, mn, mx))
    print(f"[provision] channels: {len(CHANNELS)} seeded (Beginner..Free/Clan)")


def tabs_for(item_id):
    """Client shop tab from an item number. Mirrors the working shop layout."""
    cat = item_id // 1000000
    sub = (item_id % 1000000) // 10000
    if cat == 1:          # Costume
        return 3, sub + 2
    if cat == 2:          # Weapon
        weapon_sub = {0: 1, 1: 3, 2: 4, 3: 5, 4: 6, 5: 7, 6: 6}
        return 2, weapon_sub.get(sub, 8)
    return 2, 8            # Skill + others -> render under weapon tab, sub 8


def _load_item_map():
    """Parse item.x7 into {item_id: gender}. Gender: None=0, Male=1, Female=2."""
    root = ET.parse(ITEM_X7).getroot()
    items = {}
    for it in root.findall("item"):
        key = it.get("item_key")
        if not key:
            continue
        item_id = int(key)
        base = it.find("base")
        sex = ((base.get("sex") if base is not None else "") or "").strip().lower()
        gender = 0 if sex in ("", "unisex") else (1 if sex == "man" else 2)
        items[item_id] = gender
    return items


def apply_shop():
    items = _load_item_map()

    with connect("game") as conn, conn.cursor() as cur:
        # Disable FK checks for the reload (like FOREIGN_KEY_CHECKS=0 in MySQL).
        cur.execute("SET session_replication_role = 'replica'")
        try:
            for t in ("start_items", "shop_iteminfos", "shop_items", "shop_prices",
                      "shop_price_groups", "shop_effects", "shop_effect_groups", "shop_version"):
                cur.execute(f'DELETE FROM "{t}"')
            cur.execute('INSERT INTO "shop_effect_groups" ("Id", "Name", "PreviewEffect") VALUES (1, %s, 0)', ("None",))
            cur.execute('INSERT INTO "shop_price_groups" ("Id", "Name", "PriceType") VALUES (1, %s, 1)', ("Free",))  # 1 = PEN
            cur.execute(
                'INSERT INTO "shop_prices" '
                '("Id", "PriceGroupId", "PeriodType", "Period", "Price", "IsRefundable", "Durability", "IsEnabled") '
                "VALUES (1, 1, 1, 0, 0, TRUE, 2400000, TRUE)")  # permanent, free, high durability
            for item_id, gender in sorted(items.items()):
                main, sub = tabs_for(item_id)
                cur.execute(
                    'INSERT INTO "shop_items" '
                    '("Id", "RequiredGender", "RequiredLicense", "Colors", "UniqueColors", "RequiredLevel", '
                    '"LevelLimit", "RequiredMasterLevel", "IsOneTimeUse", "IsDestroyable", "MainTab", "SubTab") '
                    "VALUES (%s, %s, 0, 0, 0, 0, 0, 0, FALSE, TRUE, %s, %s)",
                    (item_id, gender, main, sub))
                cur.execute(
                    'INSERT INTO "shop_iteminfos" '
                    '("ShopItemId", "PriceGroupId", "EffectGroupId", "DiscountPercentage", "IsEnabled") '
                    "VALUES (%s, 1, 1, 0, TRUE)",
                    (item_id,))
            cur.execute('INSERT INTO "shop_version" ("Id", "Version") VALUES (1, %s)',
                        (str(int(time.time())),))
        finally:
            cur.execute("SET session_replication_role = 'origin'")
    print(f"[provision] shop: {len(items)} items enabled (free, permanent, correct tabs)")


def ensure_start_items():
    """Seed `start_items` (default gear granted on character creation).

    On a fresh character the client's create-character screen offers 10 starter outfits
    (5 male + 5 female) and sends the chosen outfit's item numbers in
    CharacterFirstCreateReqMessage.Items. The Game server only grants an item if a
    matching row exists in `start_items` (see AuthenticationHandler.OnHandle
    CharacterFirstCreateReqMessage). Without it the character is created but owns no
    equipment, so the client's character preview is empty and it won't let you enter a
    channel. We seed the regular counterparts of every "character creation" item
    (marked "(캐릭터 생성)" in item.x7), so whichever outfit is picked its gear is granted.

    start_items.ShopItemInfoId references shop_iteminfos.Id (the buyable listing), and
    ShopPriceId references shop_prices.Id (=1, the free permanent price seeded above).
    Idempotent: skips if rows already present.
    """
    root = ET.parse(ITEM_X7).getroot()
    import re as _re
    # Map each item to its (base_name, gender_key) so we can pair a creation variant
    # ("실크레인 (캐릭터 생성)") with its regular counterpart ("실크레인").
    items = {}
    for it in root.findall("item"):
        key = it.get("item_key")
        if not key:
            continue
        base = it.find("base")
        name = (base.get("name") if base is not None else "") or ""
        sex = ((base.get("sex") if base is not None else "") or "").strip().lower()
        items[int(key)] = (name, sex)

    def strip_creation(n):
        return _re.sub(r"\s*\(캐릭터 생성.*$", "", n).strip()

    by_base = {}
    for iid, (name, sex) in items.items():
        by_base.setdefault((strip_creation(name), sex), []).append(iid)

    starters = set()
    for iid, (name, sex) in items.items():
        if "캐릭터 생성" not in name:
            continue
        base = strip_creation(name)
        regulars = [i for i in by_base.get((base, sex), []) if i != iid]
        starters.add(regulars[0] if regulars else iid)

    starters = sorted(starters)
    if not starters:
        print("[provision] start_items: no starter items found in item.x7 (skipped)")
        return

    with connect("game") as conn, conn.cursor() as cur:
        cur.execute('SELECT COUNT(*) FROM "start_items"')
        count = cur.fetchone()[0]
        if count > 0:
            print(f"[provision] start_items: {count} already present (skipped)")
            return
        # start_items requires shop_iteminfos to exist (created by apply_shop).
        for item_id in starters:
            cur.execute('SELECT "Id" FROM "shop_iteminfos" WHERE "ShopItemId"=%s', (item_id,))
            row = cur.fetchone()
            if row is None:
                continue
            cur.execute(
                'INSERT INTO "start_items" ("ShopItemInfoId", "ShopPriceId", "Color", "RequiredSecurityLevel") '
                "VALUES (%s, 1, 0, 0)",
                (row[0],))
    print(f"[provision] start_items: {len(starters)} starter items seeded (5 male + 5 female outfits)")


def ensure_bots():
    """Create BOT_COUNT regular (SecurityLevel 0) accounts named {prefix}0..{prefix}N-1.

    The load-bot harness needs one account per concurrent bot, because the game server
    rejects a second concurrent login for the same account (TerminateOtherConnection).
    Idempotent: existing accounts are left alone, only missing ones are created.
    """
    if BOT_COUNT <= 0:
        return 0
    with connect("auth") as conn, conn.cursor() as cur:
        existing = set()
        cur.execute('SELECT "Username" FROM "accounts" WHERE "Username" LIKE %s', (BOT_PREFIX + "%",))
        for row in cur.fetchall():
            existing.add(row[0])
        created = 0
        for i in range(BOT_COUNT):
            username = f"{BOT_PREFIX}{i}"
            if username in existing:
                continue
            salt = os.urandom(PBKDF2_DKLEN)
            key = hashlib.pbkdf2_hmac("sha1", BOT_PASSWORD.encode(), salt,
                                      PBKDF2_ITERATIONS, dklen=PBKDF2_DKLEN)
            cur.execute(
                'INSERT INTO "accounts" ("Username", "Nickname", "Password", "Salt", "SecurityLevel") '
                "VALUES (%s, NULL, %s, %s, %s)",
                (username,
                 base64.b64encode(key).decode(),
                 base64.b64encode(salt).decode(),
                 0),  # regular user
            )
            created += 1
        if created:
            print(f"[provision] bots: created {created} account(s) ({BOT_PREFIX}0..{BOT_PREFIX}{BOT_COUNT-1})")
        else:
            print(f"[provision] bots: all {BOT_COUNT} account(s) already present (skipped)")
        return created


def main():
    print(f"[provision] connecting to {DB_HOST}:5432 as postgres...")
    wait_for_schema()
    if ONLY_BOTS:
        ensure_bots()
        print("[provision] done (bots only).")
        return
    ensure_admin()
    ensure_bots()
    ensure_channels()
    apply_shop()
    ensure_start_items()
    print("[provision] done. Restart the game server (make bootstrap/provision does this) to load the shop.")


if __name__ == "__main__":
    main()
