using System;
using System.Collections.Generic;

namespace Yobi.Domain.Entities
{
    public sealed class CreatorLivestreamSnapshot
    {
        public LivestreamInfo CurrentLivestream { get; }
        public IReadOnlyList<LivestreamInfo> UpcomingLivestreams { get; }

        // Only populated when the underlying livestream call happened to return it (i.e. the
        // creator had at least one live/upcoming video). Not backfilled by any extra API call -
        // if a single request doesn't have it, it's left null rather than chased down separately.
        public string Studio { get; }

        public CreatorLivestreamSnapshot(LivestreamInfo currentLivestream, IReadOnlyList<LivestreamInfo> upcomingLivestreams, string studio)
        {
            CurrentLivestream = currentLivestream;
            UpcomingLivestreams = upcomingLivestreams ?? Array.Empty<LivestreamInfo>();
            Studio = studio;
        }

        public CreatorLiveStatus LiveStatus =>
            CurrentLivestream != null
                ? CreatorLiveStatus.Live
                : UpcomingLivestreams.Count > 0
                    ? CreatorLiveStatus.Upcoming
                    : CreatorLiveStatus.None;
    }
}
