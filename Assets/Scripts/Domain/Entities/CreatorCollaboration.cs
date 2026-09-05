using System.Collections.Generic;

namespace Yobi.Domain.Entities
{
    public sealed class CreatorCollaboration
    {
        public string EventName { get; }
        public string Game { get; }
        public IReadOnlyList<string> ParticipantIds { get; }
        public string Source { get; }
        public bool NeedsManualReview { get; }

        public CreatorCollaboration(string eventName, string game, IReadOnlyList<string> participantIds, string source, bool needsManualReview)
        {
            EventName = eventName;
            Game = game;
            ParticipantIds = participantIds;
            Source = source;
            NeedsManualReview = needsManualReview;
        }
    }
}
