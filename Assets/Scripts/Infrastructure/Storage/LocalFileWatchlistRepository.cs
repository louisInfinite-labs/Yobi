using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Infrastructure.Storage
{
    public sealed class LocalFileWatchlistRepository : IWatchlistRepository
    {
        private readonly string _filePath;

        public LocalFileWatchlistRepository(string filePath = null)
        {
            _filePath = filePath ?? Path.Combine(UnityEngine.Application.persistentDataPath, "tracked_creators.json");
        }

        public Watchlist Load()
        {
            var watchlist = new Watchlist();

            if (!File.Exists(_filePath))
            {
                return watchlist;
            }

            WatchlistDto dto;
            try
            {
                var json = File.ReadAllText(_filePath);
                dto = JsonUtility.FromJson<WatchlistDto>(json);
            }
            catch (Exception ex)
            {
                // A corrupt/unreadable file must not abort Awake() before the UI wires up -
                // quarantine it (so it stops tripping this on every launch) and start empty.
                Debug.LogError($"[LocalFileWatchlistRepository] Failed to read {_filePath}, starting with an empty watchlist: {ex.Message}");
                QuarantineCorruptFile();
                return watchlist;
            }

            if (dto == null)
            {
                // An empty/malformed-but-readable file deserializes to null rather than
                // throwing - without quarantining here too, it would keep tripping this
                // fallback on every future launch instead of self-healing once.
                Debug.LogError($"[LocalFileWatchlistRepository] {_filePath} did not deserialize to a valid watchlist, starting empty.");
                QuarantineCorruptFile();
                return watchlist;
            }

            if (dto.creators == null)
            {
                return watchlist;
            }

            foreach (var creatorDto in dto.creators)
            {
                watchlist.TryAdd(new WatchedCreator(creatorDto.channelId, creatorDto.displayName, creatorDto.channelUrl, creatorDto.isEnabled));
            }

            return watchlist;
        }

        public void Save(Watchlist watchlist)
        {
            var creators = new List<CreatorDto>(watchlist.Items.Count);
            foreach (var creator in watchlist.Items)
            {
                creators.Add(new CreatorDto
                {
                    channelId = creator.ChannelId,
                    displayName = creator.DisplayName,
                    channelUrl = creator.ChannelUrl,
                    isEnabled = creator.IsEnabled,
                });
            }

            var dto = new WatchlistDto { creators = creators.ToArray() };
            var json = JsonUtility.ToJson(dto, prettyPrint: true);

            // Write to a temp file and swap it in, so a crash/power-loss mid-write can't leave
            // a truncated tracked_creators.json behind for the next Load() to choke on.
            // File.Replace (rather than delete-then-move) avoids a window where a crash between
            // the two steps would delete the last valid watchlist without the new one landing.
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(_filePath))
            {
                File.Replace(tempPath, _filePath, null);
            }
            else
            {
                File.Move(tempPath, _filePath);
            }
        }

        private void QuarantineCorruptFile()
        {
            try
            {
                var quarantinePath = _filePath + ".corrupted";
                if (File.Exists(quarantinePath))
                {
                    File.Delete(quarantinePath);
                }
                File.Move(_filePath, quarantinePath);
            }
            catch (Exception)
            {
                // Best-effort - failing to quarantine must not itself crash startup.
            }
        }

        [Serializable]
        private sealed class WatchlistDto
        {
            public CreatorDto[] creators;
        }

        [Serializable]
        private sealed class CreatorDto
        {
            public string channelId;
            public string displayName;
            public string channelUrl;
            public bool isEnabled;
        }
    }
}
