#!/usr/bin/env python3
"""Faster targeted collector: queries Holodex per-topic (server-side filter) instead of
scanning the entire past-video firehose. Reuses flatten()/mention parsing from the
original holodex_collab_collector.py.
"""
from __future__ import annotations

import hashlib
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

_GAME_TOPICS_PATH = _SCRIPT_DIR / "holodex_game_topics.json"
if _GAME_TOPICS_PATH.exists():
    # Broad list of every Holodex topic that looks like an actual game (see
    # holodex_topics_full.json for the raw /topics dump this was filtered from),
    # ordered by video count descending so the highest-value topics get processed first.
    TOPICS = json.loads(_GAME_TOPICS_PATH.read_text(encoding="utf-8"))
else:
    TOPICS = {
        "VALORANT": "valorant",
        "LoL": "League_of_Legends",
        "GTA": "GTA",
        "Street Fighter 6": "Street_Fighter",
    }

PAGE_SIZE = 50
CUTOFF = datetime(2019, 1, 1, tzinfo=timezone.utc)
OUT_DIR = Path(os.environ.get("YOBI_PROJECT_ROOT", str(_SCRIPT_DIR)))
CHECKPOINT_PATH = OUT_DIR / "topic_checkpoint.json"


def _compute_checkpoint_version() -> str:
    # Changing CUTOFF or the topic mapping must not let a stale "done" flag from an earlier
    # run silently skip a topic that now needs re-checking (e.g. an older cutoff means older
    # videos were never fetched). Offsets/rows stay valid across a version change - only the
    # "done" flags need clearing - so this only needs to be sensitive to changes that affect
    # completion correctness, not offset validity.
    #
    # Hashes the full (category label -> topic id) mapping, not just the topic ids: a
    # category-label-only rename (id unchanged) still needs to invalidate "done", because
    # already-stored rows for that topic carry the old label as primary_category and won't
    # get relabeled just by re-fetching later pages. In this project's own generated
    # holodex_game_topics.json the label always equals the topic id, so that specific
    # rename case can't happen in practice - but the mapping shape (arbitrary label -> id,
    # as in the hardcoded fallback TOPICS below) allows it, so the hash covers it anyway.
    payload = json.dumps({"cutoff": CUTOFF.isoformat(), "topics": sorted(TOPICS.items())}, sort_keys=True)
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()[:16]


CHECKPOINT_VERSION = _compute_checkpoint_version()


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

    state = {"offsets": {}, "rows": [], "version": None}
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

    if state.get("version") != CHECKPOINT_VERSION:
        stale_done_keys = [k for k in offsets if k.endswith("_done") and offsets[k]]
        if stale_done_keys:
            print(f"[INFO] CUTOFF or topic mapping changed since this checkpoint was written - "
                  f"clearing {len(stale_done_keys)} 'done' flag(s) so those topics get re-checked "
                  f"against the new cutoff (existing offsets/rows are kept)")
        for key in list(offsets.keys()):
            if key.endswith("_done"):
                offsets[key] = False

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
                if not row or not row["video_id"]:
                    continue

                vid = row["video_id"]
                existing = rows_by_id.get(vid)
                if existing:
                    # Same video reached via a second topic query (the 1600+ topic mapping
                    # means overlap is routine) - keep the row already stored (whose
                    # primary_category came from a topic processed earlier, i.e. higher video
                    # count / more central to the stream) and just record the extra category.
                    existing_cats = existing["all_categories"].split(" | ") if existing.get("all_categories") else []
                    if category not in existing_cats:
                        existing_cats.append(category)
                        existing["all_categories"] = " | ".join(existing_cats)
                else:
                    row["primary_category"] = category
                    row["all_categories"] = category
                    rows_by_id[vid] = row

            offset += len(videos)
            offsets[topic] = offset

            ordered = sorted(rows_by_id.values(), key=lambda x: x["published_at"], reverse=True)
            CHECKPOINT_PATH.write_text(json.dumps({"offsets": offsets, "rows": ordered, "version": CHECKPOINT_VERSION}, ensure_ascii=False), encoding="utf-8")

            print(f"[INFO] {category} offset={offset} candidates_total={len(ordered)} oldest={oldest}")

            if oldest and oldest < CUTOFF:
                print(f"[INFO] {category}: reached cutoff {CUTOFF.date()}")
                offsets[topic + "_done"] = True
                break

            time.sleep(2.0)

        CHECKPOINT_PATH.write_text(json.dumps({"offsets": offsets, "rows": list(rows_by_id.values()), "version": CHECKPOINT_VERSION}, ensure_ascii=False), encoding="utf-8")

    ordered = sorted(rows_by_id.values(), key=lambda x: x["published_at"], reverse=True)
    md_path = OUT_DIR / "holodex_topic_candidates.md"
    base.write_output(md_path, ordered)

    all_done = all(offsets.get(topic + "_done") for topic in TOPICS.values())
    print(f"[DONE] total candidates={len(ordered)} -> {md_path} all_topics_reached_cutoff={all_done}")
    return 0 if all_done else 1


if __name__ == "__main__":
    raise SystemExit(main())
