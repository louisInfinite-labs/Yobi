using System;
using System.IO;
using UnityEngine;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Infrastructure.Storage
{
    public sealed class LocalFileAppSettingsRepository : IAppSettingsRepository
    {
        private readonly string _filePath;

        public LocalFileAppSettingsRepository(string filePath = null)
        {
            _filePath = filePath ?? Path.Combine(UnityEngine.Application.persistentDataPath, "app_settings.json");
        }

        public AppSettings Load(AppSettings defaultSettings)
        {
            if (!File.Exists(_filePath))
            {
                Save(defaultSettings);
                return defaultSettings;
            }

            AppSettingsDto dto;
            try
            {
                var json = File.ReadAllText(_filePath);

                // JsonUtility.FromJson leaves fields the JSON doesn't mention at whatever value
                // the object already had rather than reporting "this field was missing" - seed
                // the numeric fields with NaN/a sentinel and check afterward, matching
                // LocalFileWindowPositionRepository's approach, so a truncated/edited-by-hand
                // file with missing fields is rejected rather than silently read back as 0.
                dto = new AppSettingsDto
                {
                    languageCode = null,
                    resolutionWidth = int.MinValue,
                    resolutionHeight = int.MinValue,
                    fullscreen = false,
                    soundMuted = false,
                    soundVolume = float.NaN,
                    notificationsEnabled = false,
                };
                JsonUtility.FromJsonOverwrite(json, dto);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalFileAppSettingsRepository] Failed to read {_filePath}, falling back to defaults: {ex.Message}");
                QuarantineCorruptFile();
                return defaultSettings;
            }

            if (string.IsNullOrEmpty(dto.languageCode) || dto.resolutionWidth == int.MinValue || dto.resolutionHeight == int.MinValue || float.IsNaN(dto.soundVolume))
            {
                Debug.LogError($"[LocalFileAppSettingsRepository] {_filePath} is missing required fields, falling back to defaults.");
                QuarantineCorruptFile();
                return defaultSettings;
            }

            return new AppSettings(dto.languageCode, dto.resolutionWidth, dto.resolutionHeight, dto.fullscreen, dto.soundMuted, dto.soundVolume, dto.notificationsEnabled);
        }

        public void Save(AppSettings settings)
        {
            var dto = new AppSettingsDto
            {
                languageCode = settings.LanguageCode,
                resolutionWidth = settings.ResolutionWidth,
                resolutionHeight = settings.ResolutionHeight,
                fullscreen = settings.Fullscreen,
                soundMuted = settings.SoundMuted,
                soundVolume = settings.SoundVolume,
                notificationsEnabled = settings.NotificationsEnabled,
            };
            var json = JsonUtility.ToJson(dto, prettyPrint: true);

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
        private sealed class AppSettingsDto
        {
            public string languageCode;
            public int resolutionWidth;
            public int resolutionHeight;
            public bool fullscreen;
            public bool soundMuted;
            public float soundVolume;
            public bool notificationsEnabled;
        }
    }
}
