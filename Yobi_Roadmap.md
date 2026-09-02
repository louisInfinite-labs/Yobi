# Yobi Roadmap

## Phase 1 — Holodex + Core MVP

**Goal:** Complete the full core flow: search → temporary watchlist → livestream detection → reminder.

### 1. Holodex API Setup

Flow:

```text
Holodex Login
    ↓
Account Settings
    ↓
Get Holodex API Key
    ↓
Store in local config
    ↓
Test API connection
```

Requirements:

- Holodex API key must not be committed to Git.
- Keep local config ignored by `.gitignore`.
- Keep the existing YouTube API implementation as a debug/fallback source.
- Keep Mock Mode.
- No Google OAuth yet.

### 2. Holodex Channel Search

Add a simple debug UI:

```text
Search VTuber
[ __________________ ] [ Search ]
```

Flow:

```text
Input TextField
    ↓
Holodex Search API
    ↓
Holodex database
    ↓
Search Results
```

Search must use Holodex data/quota, not YouTube `search.list`.

Example result:

```text
藍沢エマ
Channel ID: UC...
Organization: VSPO
[ Add ]
```

### 3. Temporary Watchlist

From search results, press `Add` to add a creator to an in-memory watchlist.

Example:

```text
Temporary Watchlist

1. 藍沢エマ
2. 常闇トワ
3. 輪堂千速
```

Phase 1 storage is temporary:

```text
Play
→ Add creators
→ Watchlist exists

Stop
→ Watchlist cleared
```

Permanent storage is not required yet.

#### Duplicate Add Prevention

Use the creator/channel ID as the unique key.

If the creator is already in the watchlist:

```text
Already added
```

Do not add the same creator twice.

### 4. Livestream Detection

For creators in the temporary watchlist, detect:

```text
LIVE
UPCOMING
NONE
```

Expected first-line debug status:

```text
[Holodex][LIVE]
[Holodex][UPCOMING]
[Holodex][NONE]
```

For `UPCOMING`, only include scheduled livestreams within the next 24 hours.

Example:

```text
[Holodex][UPCOMING]
Channel: XXXXX
Title: XXXXX
Scheduled Start: 2026-08-27 23:00
Video ID: XXXXX
URL: XXXXX
```

### 5. Data Sources

Keep support for:

```text
Holodex
YouTube
Mock
```

Purpose:

- **Holodex** — primary real data source.
- **YouTube** — debug/comparison/future fallback.
- **Mock** — zero-network testing.

Real/Mock should use the same Presentation entry point rather than swapping separate MonoBehaviours.

### 6. Reminder Engine

Support:

- No reminders.
- One reminder.
- Two reminders.

Inspector/debug settings:

```text
Enable Reminder 1
Reminder 1 Lead Time In Minutes

Enable Reminder 2
Reminder 2 Lead Time In Minutes
```

Rules:

- All configurable timing values use **minutes**.
- Negative values are invalid.
- If both reminders are enabled, they must differ by at least 1 minute.
- Each reminder may trigger only once per livestream.
- Missed reminders are not sent late.
- Reminder timing is calculated locally.
- Reminder timing must not cause extra Holodex/YouTube API calls.

Duplicate reminder example:

```text
Stream: 09:15
Reminder: 5 minutes before

09:10:00 → notify once
09:10:05 → do not notify again
09:10:10 → do not notify again
```

### 7. Phase 1 Debug UI

The UI only needs to be functional.

Example:

```text
┌──────────────────────────────────┐
│ Yobi - Debug                     │
│                                  │
│ Search VTuber                    │
│ [ 藍沢エマ____________ ] [Search]│
│                                  │
│ Search Results                   │
│ 藍沢エマ                  [Add]   │
│                                  │
│ Temporary Watchlist              │
│ 藍沢エマ          UPCOMING       │
│ 常闇トワ          LIVE           │
└──────────────────────────────────┘
```

### Phase 1 Definition of Done

```text
Holodex API connection
    ↓
Search VTuber by text
    ↓
Show search results
    ↓
Add to temporary watchlist
    ↓
Prevent duplicate add
    ↓
Check LIVE / UPCOMING / NONE
    ↓
Run local reminder logic
    ↓
Console/debug output
```

Mock Mode must also verify:

- LIVE / UPCOMING / NONE.
- 0 / 1 / 2 reminders.
- Duplicate reminder prevention.
- Zero external API calls.

---

## Phase 1.5 — Local AI Prototype

**Goal:** Build the AI foundation before Phase 2, without depending on model training yet.

### Local AI Layer

Initial architecture:

```text
Unity / Yobi
    ↓
Local AI Client
    ↓
Local small LLM
    ↓
Structured JSON
```

Possible local runtimes:

- Ollama
- llama.cpp
- MLX

A MacBook Air M3 16GB is sufficient for experimenting with small quantized models for query parsing/entity matching.

### Query Parser

Example:

```text
Input:
立川 sf6 twitch
```

Expected structured output:

```json
{
  "intent": "find_creator",
  "creator": "立川",
  "game": "SF6",
  "platform": "Twitch"
}
```

Another example:

```text
エマとコラボしたSF6ストリーマー
```

Expected concept:

```text
intent = related_creator_search
source_creator = 藍沢エマ
relation = collaboration
game = SF6
target_type = streamer
```

### Creator Knowledge Data

Do not rely on the LLM memorizing creator facts.

Start accumulating structured data:

```text
creator aliases
platform accounts
games
collaboration relationships
query examples
corrections
evaluation cases
```

Example relationship:

```text
藍沢エマ
    ↓ collaboration / SF6
立川
    ↓
Twitch account
```

### Development Feedback

During Phase 2/3 testing:

```text
AI result
↓
Correct / Wrong
↓
Save correction
↓
Improve creator data / evaluation dataset
```

Do not retrain the model after every correction.

Most creator facts should update the knowledge database immediately; model fine-tuning should only be considered after enough high-quality query examples have accumulated.

---

## Phase 2 — Usable Desktop UI + Permanent Storage

**Goal:** Turn the prototype into a daily-usable macOS desktop application.

### Formal UI

Add:

- Search UI.
- Search result list.
- Watchlist UI.
- LIVE / UPCOMING / NONE status.
- Stream title/start time.
- Open stream button.

### Permanent Storage

Temporary runtime watchlist becomes persistent local storage.

Required:

- Add creator.
- Remove creator.
- Enable/disable creator.
- Save reminder settings.
- Restore data after restarting the app.

Possible local format:

```text
tracked_creators.json
```

### Reminder Settings UI

Move debug reminder controls from Inspector into the app UI.

### Holodex Refresh

Add controlled polling.

Example:

```text
App start
    ↓
Immediate refresh
    ↓
Wait configured interval
    ↓
Refresh again
```

Reminder timing remains local and separate from API polling.

### macOS Notifications

Replace Console-only reminders with real macOS notifications.

Flow:

```text
Reminder Event
    ↓
macOS Notification
    ↓
Click
    ↓
Open stream URL
```

Request only the minimum notification permission required.

### AI Testing Integration

Expose the local AI prototype through a small development/search UI so it can be tested throughout Phase 2.

Use real feedback to improve:

- aliases,
- relationships,
- query examples,
- ranking,
- evaluation cases.

---

## Phase 3 — Account Import + Twitch + Creator Intelligence + Character

### Google / YouTube Subscription Import

Offer two channel-add routes.

#### Route A — Holodex Search

```text
Search Holodex
↓
No Google login
↓
Add creator
```

#### Route B — Import from YouTube

```text
Google OAuth
↓
subscriptions.list
↓
Get subscribed Channel IDs
↓
Match against Holodex database
↓
Add supported creators to Yobi
```

The subscription import uses YouTube quota only for importing/syncing the user's subscriptions.

Daily LIVE/UPCOMING monitoring should prefer Holodex rather than issuing YouTube searches for every subscription.

### Twitch Integration

Introduce Twitch Helix API.

A creator may have multiple platform accounts:

```text
Creator
├─ YouTube / Holodex
└─ Twitch
```

Normalize platform status into:

```text
LIVE
UPCOMING
NONE
```

Twitch scheduled streams can reuse the existing reminder engine.

If a Twitch streamer has no schedule and suddenly goes live, trigger a go-live notification.

### Creator Discovery / AI

Use AI + structured creator knowledge to support searches such as:

```text
立川 sf6 twitch
```

or:

```text
エマとコラボしたSF6ストリーマー
```

Suggested pipeline:

```text
User query
    ↓
Query Parser
    ↓
Creator Knowledge / Relationships
    ↓
Holodex / Twitch / other providers
    ↓
Candidate ranking
    ↓
Result
```

AI should help with ambiguous entity matching and relationships, not replace reliable API/search data.

### Live2D / Pixel Character

Connect reminder/livestream events to character presentation.

```text
Livestream Event
    ↓
Reminder Engine
    ↓
System Notification + Character Reaction
```

Possible reactions:

- Motion.
- Expression.
- Voice clip.
- Dialogue bubble.

Keep character presentation independent from the livestream/domain logic.

### Desktop Companion Features

Later additions:

- Transparent window.
- Always on top.
- Drag character.
- Hide/show.
- Menu bar / tray controls.
- Character/theme switching.

---

## Phase 4 — Production AI + Cross-platform Release

**Goal:** Prepare Yobi for public distribution and decide the final AI architecture.

### AI Architecture Decision

Evaluate:

#### Option A — Local Model

- Better privacy/offline support.
- Model updates may require downloading new weights.
- Creator knowledge should still be updated separately from model weights where possible.

#### Option B — Central API / Backend

Best if Yobi should improve collectively from user-approved feedback.

```text
Users
    ↓
Yobi API
    ↓
Creator DB
Relationship DB
Feedback Dataset
LLM / Ranking
```

Corrections can improve shared knowledge without retraining after every update.

#### Option C — Hybrid

Recommended long-term direction:

- Local reminder/UI/character functionality.
- Local/offline AI optionally available.
- Central creator knowledge/search API for shared up-to-date relationships.
- Fine-tuned model only if evaluation shows a real improvement.

### Model Fine-tuning

Only consider fine-tuning after accumulating a meaningful dataset.

Compare:

```text
Base model accuracy
vs
Fine-tuned model accuracy
```

Do not fine-tune just to store creator facts; facts belong in structured knowledge/RAG.

### Windows + macOS Release

Add:

- Windows notifications.
- OS-specific abstraction.
- Startup/background support if required.
- macOS signing/notarization.
- Windows build/package.
- App icon/versioning.
- API error/rate-limit handling.
- Settings migration.
- Release documentation.
- License/attribution review.

---

# High-level Flow

```text
PHASE 1
Holodex + Core
Search → Temporary Watchlist → LIVE/UPCOMING/NONE → Reminder
        ↓
PHASE 1.5
Local AI Foundation
Query Parser → Creator Knowledge → Feedback Dataset
        ↓
PHASE 2
Usable Desktop App
Formal UI → Permanent Storage → Polling → macOS Notifications
        ↓
PHASE 3
Creator Intelligence
Google Import → Twitch → Cross-platform Matching → Live2D/Character
        ↓
PHASE 4
Production
Shared AI/Knowledge Strategy → Windows/macOS → Release
```
