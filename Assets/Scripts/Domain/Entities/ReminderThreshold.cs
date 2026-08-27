using System;

namespace Yobi.Domain.Entities
{
    public readonly struct ReminderThreshold : IEquatable<ReminderThreshold>
    {
        public string Label { get; }
        public TimeSpan LeadTime { get; }

        public ReminderThreshold(string label, TimeSpan leadTime)
        {
            Label = label;
            LeadTime = leadTime;
        }

        public bool Equals(ReminderThreshold other) => LeadTime == other.LeadTime;

        public override bool Equals(object obj) => obj is ReminderThreshold other && Equals(other);

        public override int GetHashCode() => LeadTime.GetHashCode();
    }
}
