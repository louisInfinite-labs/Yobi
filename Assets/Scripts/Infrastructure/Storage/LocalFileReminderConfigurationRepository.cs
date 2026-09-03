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

            var json = File.ReadAllText(_filePath);
            var dto = JsonUtility.FromJson<ReminderConfigurationDto>(json);
            if (dto == null)
            {
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
            File.WriteAllText(_filePath, json);
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
