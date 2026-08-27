using System;

namespace Yobi.Domain.Entities
{
    public sealed class LivestreamInfo
    {
        public string ChannelName { get; }
        public string Title { get; }
        public DateTime ScheduledStartUtc { get; }
        public string VideoId { get; }
        public string Url => $"https://www.youtube.com/watch?v={VideoId}";

        public LivestreamInfo(string channelName, string title, DateTime scheduledStartUtc, string videoId)
        {
            ChannelName = channelName;
            Title = title;
            ScheduledStartUtc = scheduledStartUtc;
            VideoId = videoId;
        }
    }
}
