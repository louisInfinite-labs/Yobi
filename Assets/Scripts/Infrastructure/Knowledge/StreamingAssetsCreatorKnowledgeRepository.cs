using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;
using Yobi.Infrastructure.Http;

namespace Yobi.Infrastructure.Knowledge
{
    public sealed class StreamingAssetsCreatorKnowledgeRepository : ICreatorKnowledgeRepository
    {
        private readonly string _url;

        public StreamingAssetsCreatorKnowledgeRepository(string url = null)
        {
            var path = url ?? Path.Combine(UnityEngine.Application.streamingAssetsPath, "CreatorKnowledge", "creator_knowledge.v1.json");

            // Application.streamingAssetsPath is a plain filesystem path on Standalone (needs
            // a file:// scheme to be a valid UnityWebRequest URL) but is already a properly
            // schemed URL on Android (jar:file://...) and WebGL (https://...) - going through
            // UnityWebRequest unconditionally, rather than File.ReadAllText, is what makes this
            // repository actually work on those platforms instead of silently returning empty.
            _url = path.Contains("://") ? path : "file://" + path;
        }

        public async Task<CreatorKnowledgeBase> LoadAsync(CancellationToken cancellationToken)
        {
            string json;
            try
            {
                using var request = UnityWebRequest.Get(_url);
                json = await UnityWebRequestAsync.SendAsync(request, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // A missing/unreadable knowledge base file must not crash the caller - the app
                // is still usable (just unable to ground any AI answers) until the data ships
                // or gets regenerated, same "fail empty, not fail loud" convention as the other
                // repositories in this project.
                Debug.LogError($"[StreamingAssetsCreatorKnowledgeRepository] Failed to load {_url}: {ex.Message}");
                return Empty();
            }

            RootDto dto;
            try
            {
                dto = JsonUtility.FromJson<RootDto>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StreamingAssetsCreatorKnowledgeRepository] Failed to parse {_url}: {ex.Message}");
                return Empty();
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

        private static CreatorKnowledgeBase Empty()
        {
            return new CreatorKnowledgeBase(new List<CreatorProfile>(), new List<CreatorCollaboration>());
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
