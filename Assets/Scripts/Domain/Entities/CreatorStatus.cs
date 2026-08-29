using System;
using System.Collections.Generic;

namespace Yobi.Domain.Entities
{
    public sealed class CreatorStatus
    {
        public string ChannelId { get; }
        public string ChannelName { get; }
        public string ChannelUrl { get; }
        public string Studio { get; }
        public bool IsWatchlisted { get; }
        public CreatorLiveStatus LiveStatus { get; }
        public LivestreamInfo CurrentLivestream { get; }
        public IReadOnlyList<LivestreamInfo> UpcomingLivestreams { get; }

        public CreatorStatus(
            string channelId,
            string channelName,
            string channelUrl,
            string studio,
            bool isWatchlisted,
            CreatorLiveStatus liveStatus,
            LivestreamInfo currentLivestream,
            IReadOnlyList<LivestreamInfo> upcomingLivestreams)
        {
            ChannelId = channelId;
            ChannelName = channelName;
            ChannelUrl = channelUrl;
            Studio = studio;
            IsWatchlisted = isWatchlisted;
            LiveStatus = liveStatus;
            CurrentLivestream = currentLivestream;
            UpcomingLivestreams = upcomingLivestreams ?? Array.Empty<LivestreamInfo>();
        }
    }
}
