using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Infrastructure.Knowledge
{
    public sealed class StreamingAssetsCreatorKnowledgeRepository : ICreatorKnowledgeRepository
    {
        private readonly string _filePath;

        public StreamingAssetsCreatorKnowledgeRepository(string filePath = null)
        {
            _filePath = filePath ?? Path.Combine(UnityEngine.Application.streamingAssetsPath, "CreatorKnowledge", "creator_knowledge.v1.json");
        }

        public CreatorKnowledgeBase Load()
        {
            if (!File.Exists(_filePath))
            {
                // A missing knowledge base file must not crash Awake() - the app is still
                // usable (just unable to ground any AI answers) until the data ships or gets
                // regenerated, same "fail empty, not fail loud" convention as the other
                // repositories in this project.
                Debug.LogError($"[StreamingAssetsCreatorKnowledgeRepository] {_filePath} not found; starting with an empty knowledge base.");
                return new CreatorKnowledgeBase(new List<CreatorProfile>(), new List<CreatorCollaboration>());
            }

            RootDto dto;
            try
            {
                var json = File.ReadAllText(_filePath);
                dto = JsonUtility.FromJson<RootDto>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StreamingAssetsCreatorKnowledgeRepository] Failed to parse {_filePath}: {ex.Message}");
                return new CreatorKnowledgeBase(new List<CreatorProfile>(), new List<CreatorCollaboration>());
            }

            var creators = (dto?.creators ?? Array.Empty<CreatorDto>())
                .Where(c => !string.IsNullOrEmpty(c.id))
                .Select(c => new CreatorProfile(c.id, c.names ?? Array.Empty<string>(), c.org, c.games ?? Array.Empty<string>()))
                .ToList();

            var collaborations = (dto?.collaborations ?? Array.Empty<CollaborationDto>())
                .Select(c => new CreatorCollaboration(c.@event, c.game, c.participants ?? Array.Empty<string>(), c.source, c.needs_manual_review))
                .ToList();

            return new CreatorKnowledgeBase(creators, collaborations);
        }

        [Serializable]
        private sealed class RootDto
        {
            public CreatorDto[] creators;
            public CollaborationDto[] collaborations;
        }

        [Serializable]
        private sealed class CreatorDto
        {
            public string id;
            public string[] names;
            public string org;
            public string[] games;
        }

        [Serializable]
        private sealed class CollaborationDto
        {
            // Field name intentionally mirrors the JSON wire format (JsonUtility matches JSON
            // keys to field names verbatim); `@` only escapes the C# reserved word at the
            // source level and is not part of the compiled member name.
            public string @event;
            public string game;
            public string[] participants;
            public string source;
            public bool needs_manual_review;
        }
    }
}
