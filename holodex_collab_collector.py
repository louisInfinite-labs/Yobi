#!/usr/bin/env python3
"""Collect likely Japanese VTuber collaboration videos from Holodex API v2.

The API key is read only from the HOLODEX_API_KEY environment variable.
Results are written as Markdown by default for convenient use in AI pipelines.
"""

from __future__ import annotations

import argparse
import csv
import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any


API_URL = "https://holodex.net/api/v2/videos"
PAGE_SIZE = 50

CATEGORY_PATTERNS: list[tuple[str, list[str]]] = [
    ("Street Fighter 6", [r"street\s*fighter\s*6", r"streetfighter6", r"\bsf\s*6\b", r"スト6", r"ストリートファイター\s*6"]),
    ("VALORANT", [r"\bvalorant\b", r"\bvalo\b", r"ヴァロラント", r"ヴァロ"]),
    ("LoL", [r"league\s*of\s*legends", r"リーグ[・･\s]*オブ[・･\s]*レジェンド", r"(?<![a-z])lol(?![a-z])"]),
    ("GTA", [r"grand\s*theft\s*auto", r"グランド[・･\s]*セフト[・･\s]*オート", r"(?<![a-z])gta\s*(?:5|v)?(?![a-z])", r"vcr\s*gta"]),
    ("雑談", [r"雑談", r"ざつだん", r"zatsudan", r"just\s*chatting", r"free\s*talk", r"おしゃべり", r"トーク"]),
    ("ASMR", [r"\basmr\b", r"耳かき", r"囁き", r"ささやき", r"睡眠導入"]),
    ("凸", [r"凸待ち", r"逆凸", r"アポなし凸", r"突撃", r"call[ -]?in", r"totsu"]),
]

CLIP_PATTERNS = [
    r"切り抜き", r"切抜き", r"切りぬき", r"clip(?:ped)?", r"highlights?",
    r"精華", r"精华", r"剪輯", r"剪辑", r"翻訳", r"翻譯", r"翻译",
]

SHORT_PATTERNS = [r"#shorts?\b", r"\bshorts?\b", r"ショート"]

SUBTITLE_PATTERNS: list[tuple[str, list[str]]] = [
    ("日文字幕", [r"日本語字幕", r"日文字幕", r"日字幕", r"jp\s*sub(?:title)?s?", r"japanese\s*sub(?:title)?s?"]),
    ("中文字幕", [r"中文字幕", r"中字", r"中文翻訳", r"中文翻譯", r"中文翻译", r"繁中", r"簡中", r"简中", r"熟肉", r"(?:chi|chs|cht|zh)\s*sub(?:title)?s?"]),
    ("英字", [r"英語字幕", r"英文字幕", r"英字", r"英訳", r"英譯", r"eng\s*sub(?:title)?s?", r"english\s*sub(?:title)?s?"]),
]

COLLAB_HINTS = re.compile(
    r"(?:コラボ|collab(?:oration)?|with\s+|w/|ゲスト|guest|参加者|メンバー|対談|座談会|凸待ち|逆凸|vcr|crカップ|大会)",
    re.IGNORECASE,
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--years", type=float, default=3.0, help="Years to look back (default: 3)")
    parser.add_argument("--lang", default="all", help="Holodex language filter (default: all, to retain translated clips)")
    parser.add_argument("--max-pages", type=int, default=400, help="Safety cap, 50 videos per page (default: 400)")
    parser.add_argument("--delay", type=float, default=0.6, help="Seconds between API calls (default: 0.6)")
    parser.add_argument("--output", default="holodex_collab_candidates.md", help="Output .md or .csv path")
    parser.add_argument("--checkpoint", default="holodex_collab_checkpoint.json", help="Resume checkpoint path")
    parser.add_argument("--keep-no-mentions", action="store_true", help="Keep title-based collab candidates without Holodex mentions")
    return parser.parse_args()


def get_json(params: dict[str, str | int], api_key: str, timeout: int = 45) -> Any:
    url = API_URL + "?" + urllib.parse.urlencode(params)
    req = urllib.request.Request(
        url,
        headers={"X-APIKEY": api_key, "User-Agent": "holodex-collab-dataset/1.0"},
    )
    with urllib.request.urlopen(req, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def video_time(video: dict[str, Any]) -> datetime | None:
    for key in ("start_actual", "start_scheduled", "available_at", "published_at"):
        value = video.get(key)
        if not value:
            continue
        try:
            return datetime.fromisoformat(str(value).replace("Z", "+00:00")).astimezone(timezone.utc)
        except ValueError:
            pass
    return None


def categories(text: str) -> list[str]:
    found = [name for name, patterns in CATEGORY_PATTERNS if any(re.search(p, text, re.IGNORECASE) for p in patterns)]
    return found or ["未分類"]


def matches_any(text: str, patterns: list[str]) -> bool:
    return any(re.search(pattern, text, re.IGNORECASE) for pattern in patterns)


def content_format(video: dict[str, Any], text: str) -> tuple[str, str]:
    raw_type = str(video.get("type") or "").lower()
    duration_raw = video.get("duration")
    try:
        duration = int(duration_raw) if duration_raw not in (None, "") else None
    except (TypeError, ValueError):
        duration = None
    clip_evidence = raw_type == "clip" or matches_any(text, CLIP_PATTERNS)
    short_evidence = matches_any(text, SHORT_PATTERNS)
    if duration is not None and duration <= 90 and (clip_evidence or short_evidence):
        return "SHORT", "duration<=90_and_clip_or_short_metadata"
    if clip_evidence and duration is not None and duration > 90:
        return "純切り抜き", "duration>90_and_clip_metadata"
    if clip_evidence:
        return "切り抜き（片長不明）", "clip_metadata_without_duration"
    if short_evidence:
        return "SHORT（片長未確認）", "short_metadata_without_usable_duration"
    return "完整直播／一般影片", "no_clip_or_short_metadata"


def subtitle_languages(text: str) -> tuple[list[str], str]:
    found = [name for name, patterns in SUBTITLE_PATTERNS if matches_any(text, patterns)]
    return (found or ["不明"], "title_description_or_channel_keywords" if found else "no_reliable_metadata")


def channel_name(channel: dict[str, Any] | None) -> str:
    if not channel:
        return ""
    return str(channel.get("english_name") or channel.get("name") or "").strip()


def mention_channels(video: dict[str, Any]) -> list[dict[str, Any]]:
    raw = video.get("mentions") or []
    return [item for item in raw if isinstance(item, dict)]


def collaboration_type(mentions: list[dict[str, Any]]) -> str:
    if not mentions:
        return "推定合作・對象未確認"
    types = {str(item.get("type") or "").lower() for item in mentions}
    if types and types <= {"vtuber"}:
        return "VTuber × VTuber"
    # Holodex channel metadata does not consistently distinguish streamers from pro players.
    return "VTuber × 外部創作者（要二次分類）"


def flatten(video: dict[str, Any], cutoff: datetime, keep_no_mentions: bool) -> dict[str, str] | None:
    when = video_time(video)
    if when and when < cutoff:
        return None
    title = str(video.get("title") or "").strip()
    description = str(video.get("description") or "").strip()
    mentions = mention_channels(video)
    has_title_hint = bool(COLLAB_HINTS.search(title + "\n" + description))
    uploader = video.get("channel") if isinstance(video.get("channel"), dict) else {}
    uploader_type = str(uploader.get("type") or "").lower()
    # Original VTuber uploads need at least one other mentioned channel. Clips
    # from non-VTuber uploaders need at least two participants to avoid treating
    # ordinary solo clips as collaborations.
    enough_mentions = len(mentions) >= (1 if uploader_type == "vtuber" else 2)
    if not enough_mentions and not (keep_no_mentions and has_title_hint):
        return None
    labels = categories(title + "\n" + description + "\n" + str(video.get("topic_id") or ""))
    evidence_text = "\n".join([
        title,
        description,
        channel_name(uploader),
        " ".join(channel_name(item) for item in mentions),
    ])
    format_label, format_evidence = content_format(video, evidence_text)
    subtitle_labels, subtitle_evidence = subtitle_languages(evidence_text)
    video_id = str(video.get("id") or "")
    return {
        "video_id": video_id,
        "youtube_url": f"https://www.youtube.com/watch?v={video_id}" if video_id else "",
        "holodex_url": f"https://holodex.net/watch/{video_id}" if video_id else "",
        "title": title,
        "published_at": when.isoformat() if when else "",
        "video_type": str(video.get("type") or ""),
        "duration_seconds": str(video.get("duration") or ""),
        "content_format": format_label,
        "content_format_evidence": format_evidence,
        "subtitle_languages": " | ".join(subtitle_labels),
        "subtitle_evidence": subtitle_evidence,
        "topic_id": str(video.get("topic_id") or ""),
        "uploader_name": channel_name(uploader),
        "uploader_channel_id": str(uploader.get("id") or ""),
        "uploader_org": str(uploader.get("org") or ""),
        "collaborator_names": " | ".join(channel_name(x) for x in mentions if channel_name(x)),
        "collaborator_channel_ids": " | ".join(str(x.get("id") or "") for x in mentions if x.get("id")),
        "collaborator_types_raw": " | ".join(str(x.get("type") or "") for x in mentions),
        "collaboration_type": collaboration_type(mentions),
        "primary_category": labels[0],
        "all_categories": " | ".join(labels),
        "collab_evidence": "holodex_mentions" if mentions else "title_or_description_keyword",
        "needs_manual_review": "false" if mentions else "true",
        "description": description,
        "source": "Holodex API v2",
    }


def write_csv(path: Path, rows: list[dict[str, str]]) -> None:
    fields = list(rows[0].keys()) if rows else [
        "video_id", "youtube_url", "holodex_url", "title", "published_at", "video_type",
        "duration_seconds", "content_format", "content_format_evidence", "subtitle_languages",
        "subtitle_evidence", "topic_id", "uploader_name", "uploader_channel_id", "uploader_org",
        "collaborator_names", "collaborator_channel_ids", "collaborator_types_raw",
        "collaboration_type", "primary_category", "all_categories", "collab_evidence",
        "needs_manual_review", "description", "source",
    ]
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)


def md_escape(value: str) -> str:
    return str(value).replace("\\", "\\\\").replace("|", "\\|").replace("\r", " ").replace("\n", " ").strip()


def write_markdown(path: Path, rows: list[dict[str, str]]) -> None:
    category_order = [name for name, _ in CATEGORY_PATTERNS] + ["未分類"]
    grouped: dict[str, list[dict[str, str]]] = {name: [] for name in category_order}
    for row in rows:
        grouped.setdefault(row["primary_category"], []).append(row)

    lines = [
        "# Holodex VTuber Collaboration Candidates",
        "",
        f"- Records: {len(rows)}",
        "- Source: https://holodex.net/",
        "- `needs_manual_review=true` means the collaboration was inferred from text rather than confirmed by Holodex mentions.",
        "- External creators require a second pass to distinguish streamers from professional gamers.",
        "",
    ]
    columns = [
        "published_at", "uploader_name", "collaborator_names", "collaboration_type",
        "title", "content_format", "duration_seconds", "subtitle_languages", "video_type",
        "topic_id", "all_categories", "collab_evidence",
        "needs_manual_review", "youtube_url", "holodex_url",
    ]
    labels = [
        "Date", "Uploader", "Collaborators", "Relationship", "Title", "Format", "Seconds",
        "Subtitles", "Holodex type", "Topic", "Labels", "Evidence", "Manual review", "YouTube", "Holodex",
    ]
    for category in category_order:
        category_rows = grouped.get(category, [])
        lines.extend([f"## {category} ({len(category_rows)})", ""])
        if not category_rows:
            lines.extend(["_No records._", ""])
            continue
        lines.append("| " + " | ".join(labels) + " |")
        lines.append("| " + " | ".join(["---"] * len(labels)) + " |")
        for row in category_rows:
            values = [md_escape(row.get(column, "")) for column in columns]
            lines.append("| " + " | ".join(values) + " |")
        lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def write_output(path: Path, rows: list[dict[str, str]]) -> None:
    if path.suffix.lower() == ".csv":
        write_csv(path, rows)
    else:
        write_markdown(path, rows)


def main() -> int:
    args = parse_args()
    api_key = os.environ.get("HOLODEX_API_KEY", "").strip()
    if not api_key:
        print("ERROR: Set HOLODEX_API_KEY in your environment first.", file=sys.stderr)
        return 2

    cutoff = datetime.now(timezone.utc) - timedelta(days=365.2425 * args.years)
    output = Path(args.output).resolve()
    checkpoint = Path(args.checkpoint).resolve()
    rows_by_id: dict[str, dict[str, str]] = {}
    offset = 0

    if checkpoint.exists():
        try:
            state = json.loads(checkpoint.read_text(encoding="utf-8"))
            offset = int(state.get("offset", 0))
            for row in state.get("rows", []):
                if isinstance(row, dict) and row.get("video_id"):
                    rows_by_id[str(row["video_id"])] = {str(k): str(v) for k, v in row.items()}
            print(f"[INFO] Resuming at offset {offset}; {len(rows_by_id)} candidates loaded")
        except (ValueError, OSError):
            print("[WARN] Invalid checkpoint ignored", file=sys.stderr)

    for page in range(args.max_pages):
        params = {
            "status": "past",
            "lang": args.lang,
            "include": "mentions,description",
            "sort": "available_at",
            "order": "desc",
            "limit": PAGE_SIZE,
            "offset": offset,
        }
        try:
            payload = get_json(params, api_key)
        except urllib.error.HTTPError as exc:
            print(f"ERROR: Holodex HTTP {exc.code}: {exc.reason}", file=sys.stderr)
            return 3
        except (urllib.error.URLError, TimeoutError) as exc:
            print(f"ERROR: Network request failed: {exc}", file=sys.stderr)
            return 4

        videos = payload.get("items", []) if isinstance(payload, dict) else payload
        if not isinstance(videos, list) or not videos:
            print("[INFO] No more videos")
            break

        oldest = None
        for video in videos:
            if not isinstance(video, dict):
                continue
            when = video_time(video)
            if when and (oldest is None or when < oldest):
                oldest = when
            row = flatten(video, cutoff, args.keep_no_mentions)
            if row and row["video_id"]:
                rows_by_id[row["video_id"]] = row

        offset += len(videos)
        ordered = sorted(rows_by_id.values(), key=lambda x: x["published_at"], reverse=True)
        checkpoint.write_text(json.dumps({"offset": offset, "rows": ordered}, ensure_ascii=False), encoding="utf-8")
        write_output(output, ordered)
        print(f"[INFO] page={page + 1} scanned={offset} candidates={len(ordered)} oldest={oldest}")

        if oldest and oldest < cutoff:
            print(f"[INFO] Reached cutoff {cutoff.date()}")
            break
        time.sleep(max(args.delay, 0.1))

    ordered = sorted(rows_by_id.values(), key=lambda x: x["published_at"], reverse=True)
    write_output(output, ordered)
    print(f"[DONE] {len(ordered)} rows -> {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
