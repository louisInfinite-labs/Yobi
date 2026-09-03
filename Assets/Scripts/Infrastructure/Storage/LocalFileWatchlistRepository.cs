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

            var json = File.ReadAllText(_filePath);
            var dto = JsonUtility.FromJson<WatchlistDto>(json);
            if (dto?.creators == null)
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
            File.WriteAllText(_filePath, json);
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
