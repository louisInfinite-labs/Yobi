#!/usr/bin/env python3
"""Faster targeted collector: queries Holodex per-topic (server-side filter) instead of
scanning the entire past-video firehose. Reuses flatten()/mention parsing from the
original holodex_collab_collector.py.
"""
from __future__ import annotations

import json
import os
import sys
import time
import urllib.error
from datetime import datetime, timezone
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(_SCRIPT_DIR))
import holodex_collab_collector as base  # noqa: E402

TOPICS = {
    "VALORANT": "valorant",
    "LoL": "League_of_Legends",
    "GTA": "GTA",
    "Street Fighter 6": "Street_Fighter",
}

PAGE_SIZE = 50
CUTOFF = datetime(2023, 1, 1, tzinfo=timezone.utc)
OUT_DIR = Path(os.environ.get("YOBI_PROJECT_ROOT", str(_SCRIPT_DIR)))
CHECKPOINT_PATH = OUT_DIR / "topic_checkpoint.json"


def get_json_by_topic(topic: str, offset: int, api_key: str):
    params = {
        "status": "past",
        "topic": topic,
        "include": "mentions,description",
        "sort": "available_at",
        "order": "desc",
        "limit": PAGE_SIZE,
        "offset": offset,
    }
    return base.get_json(params, api_key)


def main() -> int:
    api_key = os.environ.get("HOLODEX_API_KEY", "").strip()
    if not api_key:
        print("ERROR: set HOLODEX_API_KEY", file=sys.stderr)
        return 2

    state = {"offsets": {}, "rows": []}
    if CHECKPOINT_PATH.exists():
        try:
            state = json.loads(CHECKPOINT_PATH.read_text(encoding="utf-8"))
        except (ValueError, OSError):
            pass

    required_keys = ("video_id", "published_at", "primary_category")
    rows_by_id = {
        r["video_id"]: r
        for r in state.get("rows", [])
        if isinstance(r, dict) and all(r.get(k) is not None for k in required_keys)
    }
    offsets = state.get("offsets", {})

    for category, topic in TOPICS.items():
        offset = offsets.get(topic, 0)
        done = offsets.get(topic + "_done", False)
        if done:
            print(f"[INFO] {category} ({topic}) already marked done, skipping")
            continue

        print(f"[INFO] === {category} (topic={topic}) starting at offset={offset} ===")
        while True:
            try:
                payload = get_json_by_topic(topic, offset, api_key)
            except urllib.error.HTTPError as exc:
                print(f"ERROR: HTTP {exc.code} for {topic} at offset {offset}: {exc.reason}", file=sys.stderr)
                break
            except (urllib.error.URLError, TimeoutError) as exc:
                print(f"ERROR: network failure for {topic} at offset {offset}: {exc}", file=sys.stderr)
                break

            videos = payload if isinstance(payload, list) else payload.get("items", [])
            if not videos:
                if offset == 0:
                    # Empty on the very first page likely means an invalid topic id rather
                    # than a genuinely exhausted topic - don't mark it done, so this surfaces
                    # as a real failure instead of silently "succeeding" with zero rows.
                    print(f"ERROR: {category} ({topic}) returned zero results on the first page - check the topic id", file=sys.stderr)
                    break
                print(f"[INFO] {category}: no more results, done")
                offsets[topic + "_done"] = True
                break

            oldest = None
            for video in videos:
                if not isinstance(video, dict):
                    continue
                when = base.video_time(video)
                if when and (oldest is None or when < oldest):
                    oldest = when
                row = base.flatten(video, CUTOFF, keep_no_mentions=False)
                if row and row["video_id"]:
                    row["primary_category"] = category
                    row["all_categories"] = category
                    rows_by_id[row["video_id"]] = row

            offset += len(videos)
            offsets[topic] = offset

            ordered = sorted(rows_by_id.values(), key=lambda x: x["published_at"], reverse=True)
            CHECKPOINT_PATH.write_text(json.dumps({"offsets": offsets, "rows": ordered}, ensure_ascii=False), encoding="utf-8")

            print(f"[INFO] {category} offset={offset} candidates_total={len(ordered)} oldest={oldest}")

            if oldest and oldest < CUTOFF:
                print(f"[INFO] {category}: reached cutoff {CUTOFF.date()}")
                offsets[topic + "_done"] = True
                break

            time.sleep(2.0)

        CHECKPOINT_PATH.write_text(json.dumps({"offsets": offsets, "rows": list(rows_by_id.values())}, ensure_ascii=False), encoding="utf-8")

    ordered = sorted(rows_by_id.values(), key=lambda x: x["published_at"], reverse=True)
    md_path = OUT_DIR / "holodex_topic_candidates.md"
    base.write_output(md_path, ordered)

    all_done = all(offsets.get(topic + "_done") for topic in TOPICS.values())
    print(f"[DONE] total candidates={len(ordered)} -> {md_path} all_topics_reached_cutoff={all_done}")
    return 0 if all_done else 1


if __name__ == "__main__":
    raise SystemExit(main())
