using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Infrastructure.Config
{
    public sealed class LocalFileChannelConfigProvider : IChannelConfigProvider
    {
        private readonly string _configFilePath;
        private ConfigDto _cachedConfig;

        public LocalFileChannelConfigProvider(string configFilePath = null)
        {
            _configFilePath = configFilePath ?? Path.Combine(UnityEngine.Application.dataPath, "..", "config.local.json");
        }

        public string GetApiKey()
        {
            return LoadConfig().youtubeApiKey;
        }

        public string GetHolodexApiKey()
        {
            return LoadConfig().holodexApiKey;
        }

        public IReadOnlyList<YouTubeChannel> GetChannels()
        {
            var config = LoadConfig();
            var channels = new List<YouTubeChannel>();
            if (config.channels == null)
            {
                return channels;
            }

            foreach (var dto in config.channels)
            {
                channels.Add(new YouTubeChannel(dto.name, dto.handle, dto.channelId));
            }

            return channels;
        }

        private ConfigDto LoadConfig()
        {
            if (_cachedConfig != null)
            {
                return _cachedConfig;
            }

            if (!File.Exists(_configFilePath))
            {
                throw new FileNotFoundException($"config.local.json not found at {_configFilePath}");
            }

            var json = File.ReadAllText(_configFilePath);
            _cachedConfig = JsonUtility.FromJson<ConfigDto>(json);
            return _cachedConfig;
        }

        [Serializable]
        private sealed class ConfigDto
        {
            public string youtubeApiKey;
            public ChannelDto[] channels;
            public string holodexApiKey;
        }

        [Serializable]
        private sealed class ChannelDto
        {
            public string name;
            public string handle;
            public string channelId;
        }
    }
}
