using System;
using System.IO;
using UnityEngine;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Infrastructure.Storage
{
    public sealed class LocalFileReminderConfigurationRepository : IReminderConfigurationRepository
    {
        private readonly string _filePath;

        public LocalFileReminderConfigurationRepository(string filePath = null)
        {
            _filePath = filePath ?? Path.Combine(UnityEngine.Application.persistentDataPath, "reminder_settings.json");
        }

        public ReminderConfiguration Load(ReminderConfiguration defaultConfiguration)
        {
            if (!File.Exists(_filePath))
            {
                Save(defaultConfiguration);
                return defaultConfiguration;
            }

            ReminderConfigurationDto dto;
            try
            {
                var json = File.ReadAllText(_filePath);
                dto = JsonUtility.FromJson<ReminderConfigurationDto>(json);
            }
            catch (Exception ex)
            {
                // A corrupt/unreadable file must not abort Awake() before the UI wires up -
                // quarantine it (so it stops tripping this on every launch) and fall back to defaults.
                Debug.LogError($"[LocalFileReminderConfigurationRepository] Failed to read {_filePath}, falling back to defaults: {ex.Message}");
                QuarantineCorruptFile();
                return defaultConfiguration;
            }

            if (dto == null)
            {
                // An empty/malformed-but-readable file deserializes to null rather than
                // throwing - without quarantining here too, it would keep tripping this
                // fallback on every future launch instead of self-healing once.
                Debug.LogError($"[LocalFileReminderConfigurationRepository] {_filePath} did not deserialize to a valid configuration, falling back to defaults.");
                QuarantineCorruptFile();
                return defaultConfiguration;
            }

            return new ReminderConfiguration(dto.enableReminder1, dto.reminder1LeadTimeInMinutes, dto.enableReminder2, dto.reminder2LeadTimeInMinutes);
        }

        public void Save(ReminderConfiguration configuration)
        {
            var dto = new ReminderConfigurationDto
            {
                enableReminder1 = configuration.EnableReminder1,
                reminder1LeadTimeInMinutes = configuration.Reminder1LeadTimeInMinutes,
                enableReminder2 = configuration.EnableReminder2,
                reminder2LeadTimeInMinutes = configuration.Reminder2LeadTimeInMinutes,
            };

            var json = JsonUtility.ToJson(dto, prettyPrint: true);

            // Write to a temp file and swap it in, so a crash/power-loss mid-write can't leave
            // a truncated reminder_settings.json behind for the next Load() to choke on.
            // File.Replace (rather than delete-then-move) avoids a window where a crash between
            // the two steps would delete the last valid config without the new one landing.
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
        private sealed class ReminderConfigurationDto
        {
            public bool enableReminder1;
            public int reminder1LeadTimeInMinutes;
            public bool enableReminder2;
            public int reminder2LeadTimeInMinutes;
        }
    }
}
