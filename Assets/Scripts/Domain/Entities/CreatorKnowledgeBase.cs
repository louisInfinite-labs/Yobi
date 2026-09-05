using System;
using System.Collections.Generic;
using System.Linq;

namespace Yobi.Domain.Entities
{
    public sealed class CreatorKnowledgeBase
    {
        public IReadOnlyList<CreatorProfile> Creators { get; }
        public IReadOnlyList<CreatorCollaboration> Collaborations { get; }

        private readonly Dictionary<string, CreatorProfile> _creatorsById;

        public CreatorKnowledgeBase(IReadOnlyList<CreatorProfile> creators, IReadOnlyList<CreatorCollaboration> collaborations)
        {
            Creators = creators;
            Collaborations = collaborations;
            _creatorsById = creators.ToDictionary(c => c.Id, c => c);
        }

        public CreatorProfile GetById(string id)
        {
            return _creatorsById.TryGetValue(id, out var creator) ? creator : null;
        }

        // Substring match against every known alias, case-insensitive - good enough for a
        // query typed by a person who may only remember part of a name (roadmap's own
        // examples do this: "立川" alone, not a full/exact channel name).
        public IReadOnlyList<CreatorProfile> FindMentionedIn(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<CreatorProfile>();
            }

            var normalized = text.ToLowerInvariant();
            var matches = new List<CreatorProfile>();

            foreach (var creator in Creators)
            {
                foreach (var name in creator.Names)
                {
                    if (!string.IsNullOrEmpty(name) && normalized.Contains(name.ToLowerInvariant()))
                    {
                        matches.Add(creator);
                        break;
                    }
                }
            }

            return matches;
        }

        public IReadOnlyList<CreatorCollaboration> FindCollaborationsFor(string creatorId)
        {
            return Collaborations.Where(c => c.ParticipantIds.Contains(creatorId)).ToList();
        }
    }
}
