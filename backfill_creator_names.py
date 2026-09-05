#!/usr/bin/env python3
"""Backfill original-language + English names for every creator in the knowledge base.

holodex_collab_collector.py's channel_name() always prefers english_name over the channel's
own `name`, discarding the original-language name entirely when building creator_knowledge
entries. That original name was never lost from Holodex's side, though - the raw Holodex
channel_id survived into topic_checkpoint.json (uploader_channel_id/collaborator_channel_ids)
even though merge_topic_knowledge.py never used it. This script uses those ids to query
Holodex's per-channel endpoint once per creator and add every name variant as an alias, so a
search in either Japanese or English finds the same creator.

Resumable: progress is checkpointed per creator id, so a network hiccup partway through only
costs the in-flight request, not everything done before it.
"""
from __future__ import annotations

import json
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

CHECKPOINT_PATH = Path("/Users/louis/Yobi/topic_checkpoint.json")
KB_PATH = Path("/Users/louis/Yobi/Assets/StreamingAssets/CreatorKnowledge/creator_knowledge.v1.json")
PROGRESS_PATH = Path("/Users/louis/Yobi/backfill_names_progress.json")
API_URL = "https://holodex.net/api/v2/channels/{}"
REQUEST_DELAY_SECONDS = 1.5


def get_channel_info(channel_id: str, api_key: str) -> dict:
    req = urllib.request.Request(
        API_URL.format(channel_id),
        headers={"X-APIKEY": api_key, "User-Agent": "yobi-name-backfill/1.0"},
    )
    with urllib.request.urlopen(req, timeout=20) as response:
        return json.loads(response.read().decode("utf-8"))


def build_name_to_channel_id(rows: list[dict]) -> dict[str, str]:
    mapping: dict[str, str] = {}
    for row in rows:
        pairs = [(row.get("uploader_name"), row.get("uploader_channel_id"))]
        collab_names = (row.get("collaborator_names") or "").split(" | ")
        collab_ids = (row.get("collaborator_channel_ids") or "").split(" | ")
        pairs.extend(zip(collab_names, collab_ids))
        for name, channel_id in pairs:
            if name and channel_id and name not in mapping:
                mapping[name] = channel_id
    return mapping


def main() -> int:
    api_key = os.environ.get("HOLODEX_API_KEY", "").strip()
    if not api_key:
        print("ERROR: set HOLODEX_API_KEY", file=sys.stderr)
        return 2

    checkpoint = json.loads(CHECKPOINT_PATH.read_text(encoding="utf-8"))
    kb = json.loads(KB_PATH.read_text(encoding="utf-8"))
    name_to_channel_id = build_name_to_channel_id(checkpoint["rows"])

    progress: dict[str, dict] = {}
    if PROGRESS_PATH.exists():
        try:
            progress = json.loads(PROGRESS_PATH.read_text(encoding="utf-8"))
        except (ValueError, OSError):
            progress = {}

    updated = 0
    queried = 0
    for creator in kb["creators"]:
        creator_id = creator["id"]
        entry = progress.get(creator_id)

        if entry is None:
            # Try every known alias, not just names[0]: a handful of creators were entered
            # manually with the original-language name first (e.g. "葛葉" before "Kuzuha"),
            # but the checkpoint only ever recorded the english_name-preferring resolved
            # string, so checking names[0] alone missed a real match sitting under names[1].
            channel_id = next((name_to_channel_id[n] for n in creator["names"] if n in name_to_channel_id), None)
            if not channel_id:
                progress[creator_id] = {"skipped": "no_channel_id_found"}
                continue

            try:
                info = get_channel_info(channel_id, api_key)
            except urllib.error.HTTPError as exc:
                print(f"[WARN] {creator_id} ({channel_id}): HTTP {exc.code}", file=sys.stderr)
                continue
            except (urllib.error.URLError, TimeoutError) as exc:
                print(f"[WARN] {creator_id} ({channel_id}): network failure: {exc}", file=sys.stderr)
                continue

            progress[creator_id] = info
            PROGRESS_PATH.write_text(json.dumps(progress, ensure_ascii=False), encoding="utf-8")
            queried += 1
            if queried % 25 == 0:
                print(f"[INFO] queried {queried} channels so far ({creator_id}: {info.get('name')} / {info.get('english_name')})")
            time.sleep(REQUEST_DELAY_SECONDS)
            entry = info

        if entry.get("skipped"):
            continue

        changed = False
        for candidate in (entry.get("name"), entry.get("english_name")):
            if not candidate:
                continue
            # Holodex's own `name` field is sometimes already a combined "JP / EN" string
            # rather than a single name - storing that whole string as one alias means a
            # substring search for just the JP or EN half never matches it.
            for part in (p.strip() for p in candidate.split(" / ")):
                if part and part not in creator["names"]:
                    creator["names"].append(part)
                    changed = True

        if entry.get("org") and creator.get("org") in (None, "unknown"):
            creator["org"] = entry["org"]
            changed = True

        if changed:
            updated += 1

    KB_PATH.write_text(json.dumps(kb, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"[DONE] queried={queried} updated_creators={updated} total_creators={len(kb['creators'])}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
