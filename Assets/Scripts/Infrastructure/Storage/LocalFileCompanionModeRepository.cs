using System;
using System.IO;
using UnityEngine;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Infrastructure.Storage
{
    public sealed class LocalFileCompanionModeRepository : ICompanionModeRepository
    {
        private readonly string _filePath;

        public LocalFileCompanionModeRepository(string filePath = null)
        {
            _filePath = filePath ?? Path.Combine(UnityEngine.Application.persistentDataPath, "companion_mode.json");
        }

        public CompanionMode Load(CompanionMode defaultMode)
        {
            if (!File.Exists(_filePath))
            {
                Save(defaultMode);
                return defaultMode;
            }

            CompanionModeDto dto;
            try
            {
                var json = File.ReadAllText(_filePath);
                dto = JsonUtility.FromJson<CompanionModeDto>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalFileCompanionModeRepository] Failed to read {_filePath}, falling back to default: {ex.Message}");
                QuarantineCorruptFile();
                return defaultMode;
            }

            // Enum.TryParse alone would also accept a numeric string outside the enum's
            // declared values (e.g. "5"), silently returning true - Enum.IsDefined is needed to
            // actually reject that.
            if (dto == null || string.IsNullOrEmpty(dto.mode)
                || !Enum.TryParse(dto.mode, out CompanionMode parsedMode)
                || !Enum.IsDefined(typeof(CompanionMode), parsedMode))
            {
                Debug.LogError($"[LocalFileCompanionModeRepository] {_filePath} did not contain a valid mode, falling back to default.");
                QuarantineCorruptFile();
                return defaultMode;
            }

            return parsedMode;
        }

        public void Save(CompanionMode mode)
        {
            var dto = new CompanionModeDto { mode = mode.ToString() };
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
        private sealed class CompanionModeDto
        {
            public string mode;
        }
    }
}
