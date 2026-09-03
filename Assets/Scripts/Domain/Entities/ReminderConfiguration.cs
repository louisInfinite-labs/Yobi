using System;
using System.Collections.Generic;

namespace Yobi.Domain.Entities
{
    public sealed class ReminderConfiguration
    {
        public bool EnableReminder1 { get; private set; }
        public int Reminder1LeadTimeInMinutes { get; private set; }
        public bool EnableReminder2 { get; private set; }
        public int Reminder2LeadTimeInMinutes { get; private set; }

        public ReminderConfiguration(bool enableReminder1, int reminder1LeadTimeInMinutes, bool enableReminder2, int reminder2LeadTimeInMinutes)
        {
            EnableReminder1 = enableReminder1;
            Reminder1LeadTimeInMinutes = reminder1LeadTimeInMinutes;
            EnableReminder2 = enableReminder2;
            Reminder2LeadTimeInMinutes = reminder2LeadTimeInMinutes;
            Normalize();
        }

        // Mirrors the previous ReminderSettings.Normalize(): clamps negative lead times to
        // zero, and if both reminders are enabled with the same lead time, nudges reminder 2
        // by a minute so the "each reminder fires once" rule in the roadmap stays meaningful.
        private void Normalize()
        {
            if (Reminder1LeadTimeInMinutes < 0)
            {
                Reminder1LeadTimeInMinutes = 0;
            }

            if (Reminder2LeadTimeInMinutes < 0)
            {
                Reminder2LeadTimeInMinutes = 0;
            }

            if (EnableReminder1 && EnableReminder2 && Reminder1LeadTimeInMinutes == Reminder2LeadTimeInMinutes)
            {
                Reminder2LeadTimeInMinutes = Reminder1LeadTimeInMinutes > 0
                    ? Reminder1LeadTimeInMinutes - 1
                    : Reminder1LeadTimeInMinutes + 1;
            }
        }

        public IReadOnlyList<ReminderThreshold> BuildThresholds()
        {
            var thresholds = new List<ReminderThreshold>();

            if (EnableReminder1)
            {
                thresholds.Add(new ReminderThreshold("Reminder 1", TimeSpan.FromMinutes(Reminder1LeadTimeInMinutes)));
            }

            if (EnableReminder2)
            {
                thresholds.Add(new ReminderThreshold("Reminder 2", TimeSpan.FromMinutes(Reminder2LeadTimeInMinutes)));
            }

            return thresholds;
        }
    }
}
