using System;
using System.IO;
using UnityEngine;
using Yobi.Domain.Interfaces;

namespace Yobi.Infrastructure.Storage
{
    public sealed class LocalFileRoomWallpaperRepository : IRoomWallpaperRepository
    {
        private readonly string _filePath;

        public LocalFileRoomWallpaperRepository(string filePath = null)
        {
            _filePath = filePath ?? Path.Combine(UnityEngine.Application.persistentDataPath, "room_wallpaper.json");
        }

        public string Load()
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            RoomWallpaperDto dto;
            try
            {
                var json = File.ReadAllText(_filePath);
                dto = JsonUtility.FromJson<RoomWallpaperDto>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalFileRoomWallpaperRepository] Failed to read {_filePath}, ignoring saved wallpaper: {ex.Message}");
                QuarantineCorruptFile();
                return null;
            }

            if (dto == null || string.IsNullOrEmpty(dto.imageFilePath))
            {
                Debug.LogError($"[LocalFileRoomWallpaperRepository] {_filePath} did not contain a valid wallpaper path, ignoring.");
                QuarantineCorruptFile();
                return null;
            }

            // The referenced image itself may have since moved or been deleted - that's the
            // caller's concern when it tries to load the texture, not something to quarantine
            // the settings file over.
            return dto.imageFilePath;
        }

        public void Save(string imageFilePath)
        {
            var dto = new RoomWallpaperDto { imageFilePath = imageFilePath };
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
        private sealed class RoomWallpaperDto
        {
            public string imageFilePath;
        }
    }
}
