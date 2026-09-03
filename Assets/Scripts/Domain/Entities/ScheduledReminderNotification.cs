using System;

namespace Yobi.Domain.Entities
{
    public sealed class ScheduledReminderNotification
    {
        public string Id { get; }
        public string Title { get; }
        public string Body { get; }
        public string Url { get; }
        public DateTime FireAtUtc { get; }

        public ScheduledReminderNotification(string id, string title, string body, string url, DateTime fireAtUtc)
        {
            Id = id;
            Title = title;
            Body = body;
            Url = url;
            FireAtUtc = fireAtUtc;
        }
    }
}
