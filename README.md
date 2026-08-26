# Desktop Companion

Cross-platform desktop companion built with Unity.

## MVP

- Track manually selected YouTube channels
- Detect upcoming livestreams within the next 24 hours
- macOS notifications at 30 and 15 minutes before stream start
- Local channel configuration
- No Google OAuth required for MVP

## Planned

- Windows notifications
- Live2D / Unity-chan integration
- Alarm feature
- Local music player
- Synced lyrics

## Tech

- Unity 6 LTS
- C#
- YouTube Data API v3
- macOS / Windows

## Architecture

Clean Architecture:

- Domain
- Application
- Infrastructure
- Presentation

See `CLAUDE.md` for development rules.

## Setup

1. Clone repository
2. Open project using the required Unity version
3. Configure YouTube API key locally
4. Do not commit API keys or secrets

## Required Unity Version

Unity 6.3 LTS

Do not modify .claude/settings.json without explicit approval.