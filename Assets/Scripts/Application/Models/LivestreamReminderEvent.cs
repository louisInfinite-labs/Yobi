using System;
using Yobi.Domain.Entities;

namespace Yobi.Application.Models
{
    public sealed class LivestreamReminderEvent
    {
        public string ChannelName { get; }
        public string Title { get; }
        public string VideoId { get; }
        public DateTime ScheduledStartUtc { get; }
        public ReminderThreshold Threshold { get; }

        public LivestreamReminderEvent(string channelName, string title, string videoId, DateTime scheduledStartUtc, ReminderThreshold threshold)
        {
            ChannelName = channelName;
            Title = title;
            VideoId = videoId;
            ScheduledStartUtc = scheduledStartUtc;
            Threshold = threshold;
        }
    }
}
