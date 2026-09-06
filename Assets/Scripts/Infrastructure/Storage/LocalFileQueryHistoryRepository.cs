using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Infrastructure.Storage
{
    public sealed class LocalFileQueryHistoryRepository : IQueryHistoryRepository
    {
        private readonly string _filePath;

        public LocalFileQueryHistoryRepository(string filePath = null)
        {
            _filePath = filePath ?? Path.Combine(UnityEngine.Application.persistentDataPath, "search_history.json");
        }

        public QueryHistory Load()
        {
            if (!File.Exists(_filePath))
            {
                return new QueryHistory();
            }

            QueryHistoryDto dto;
            try
            {
                var json = File.ReadAllText(_filePath);
                dto = JsonUtility.FromJson<QueryHistoryDto>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalFileQueryHistoryRepository] Failed to read {_filePath}, starting with empty history: {ex.Message}");
                QuarantineCorruptFile();
                return new QueryHistory();
            }

            if (dto == null)
            {
                Debug.LogError($"[LocalFileQueryHistoryRepository] {_filePath} did not deserialize to valid history, starting empty.");
                QuarantineCorruptFile();
                return new QueryHistory();
            }

            return new QueryHistory(dto.entries);
        }

        public void Save(QueryHistory history)
        {
            var dto = new QueryHistoryDto { entries = new List<string>(history.Entries).ToArray() };
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
        private sealed class QueryHistoryDto
        {
            public string[] entries;
        }
    }
}
