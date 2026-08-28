using System;
using System.Collections.Generic;

namespace Yobi.Domain.Entities
{
    public sealed class CreatorLivestreamSnapshot
    {
        public LivestreamInfo CurrentLivestream { get; }
        public IReadOnlyList<LivestreamInfo> UpcomingLivestreams { get; }

        public CreatorLivestreamSnapshot(LivestreamInfo currentLivestream, IReadOnlyList<LivestreamInfo> upcomingLivestreams)
        {
            CurrentLivestream = currentLivestream;
            UpcomingLivestreams = upcomingLivestreams ?? Array.Empty<LivestreamInfo>();
        }

        public CreatorLiveStatus LiveStatus =>
            CurrentLivestream != null
                ? CreatorLiveStatus.Live
                : UpcomingLivestreams.Count > 0
                    ? CreatorLiveStatus.Upcoming
                    : CreatorLiveStatus.None;
    }
}
