using System;
using System.IO;
using UnityEngine;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Infrastructure.Storage
{
    public sealed class LocalFileWindowPositionRepository : IWindowPositionRepository
    {
        private readonly string _filePath;

        public LocalFileWindowPositionRepository(string filePath = null)
        {
            _filePath = filePath ?? Path.Combine(UnityEngine.Application.persistentDataPath, "window_position.json");
        }

        public WindowPosition Load()
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            WindowPositionDto dto;
            try
            {
                var json = File.ReadAllText(_filePath);

                // JsonUtility.FromJson leaves fields the JSON doesn't mention at whatever value
                // the object already had - it never reports "this field was missing". Seeding
                // both fields with NaN before overwriting, then checking for NaN afterward, is
                // what actually distinguishes an incomplete record (e.g. `{}` or `{"x":42}`,
                // which would otherwise silently read back as x/y = 0) from a real, valid one.
                dto = new WindowPositionDto { x = double.NaN, y = double.NaN };
                JsonUtility.FromJsonOverwrite(json, dto);
            }
            catch (Exception ex)
            {
                // A corrupt/unreadable file must not abort Awake() before the window shows up -
                // quarantine it (so it stops tripping this on every launch) and fall back to
                // leaving the window at its default position.
                Debug.LogError($"[LocalFileWindowPositionRepository] Failed to read {_filePath}, ignoring saved position: {ex.Message}");
                QuarantineCorruptFile();
                return null;
            }

            if (double.IsNaN(dto.x) || double.IsNaN(dto.y))
            {
                Debug.LogError($"[LocalFileWindowPositionRepository] {_filePath} is missing x/y, ignoring saved position.");
                QuarantineCorruptFile();
                return null;
            }

            return new WindowPosition(dto.x, dto.y);
        }

        public void Save(WindowPosition position)
        {
            var dto = new WindowPositionDto { x = position.X, y = position.Y };
            var json = JsonUtility.ToJson(dto, prettyPrint: true);

            // Write to a temp file and swap it in, so a crash/power-loss mid-write can't leave
            // a truncated window_position.json behind for the next Load() to choke on.
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
        private sealed class WindowPositionDto
        {
            public double x;
            public double y;
        }
    }
}
