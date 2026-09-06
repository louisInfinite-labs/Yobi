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
            _configFilePath = configFilePath ?? ResolveDefaultConfigPath();
        }

        // config.local.json holds API keys (CLAUDE.md: never commit these) so it only ever lives
        // at the project root, never inside Assets - Application.dataPath is "<project>/Assets" in
        // the Editor, one level up from which is exactly that root. A macOS standalone build's
        // dataPath instead sits inside the app bundle itself (".../Yobi.app/Contents" on this
        // Unity version, empirically - not the deeper .../Contents/Resources/Data some older
        // Unity versions used), so it takes two levels up to reach the folder actually containing
        // the .app - which is where BuildScript's PostProcessBuild step copies this file to on
        // every build. Windows dataPath ("<exeDir>/<Product>_Data") only needs the one level up,
        // same as the Editor case.
        private static string ResolveDefaultConfigPath()
        {
            var dataPath = UnityEngine.Application.dataPath;
            return UnityEngine.Application.platform == RuntimePlatform.OSXPlayer
                ? Path.Combine(dataPath, "..", "..", "config.local.json")
                : Path.Combine(dataPath, "..", "config.local.json");
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
