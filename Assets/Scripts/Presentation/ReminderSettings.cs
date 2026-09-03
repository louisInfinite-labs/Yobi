using System.Collections.Generic;
using UnityEngine;
using Yobi.Domain.Entities;

namespace Yobi.Presentation
{
    // Inspector-editable defaults only. Normalization and threshold-building rules live in
    // Yobi.Domain.Entities.ReminderConfiguration so they stay usable (and testable) outside
    // this Unity asset - e.g. once a value is loaded back from persisted storage.
    [CreateAssetMenu(fileName = "ReminderSettings", menuName = "Yobi/Reminder Settings")]
    public sealed class ReminderSettings : ScriptableObject
    {
        [SerializeField]
        private bool enableReminder1 = true;

        [SerializeField]
        private int reminder1LeadTimeInMinutes = 30;

        [SerializeField]
        private bool enableReminder2 = true;

        [SerializeField]
        private int reminder2LeadTimeInMinutes = 15;

        private void OnValidate()
        {
            var normalized = ToDomainConfiguration();
            enableReminder1 = normalized.EnableReminder1;
            reminder1LeadTimeInMinutes = normalized.Reminder1LeadTimeInMinutes;
            enableReminder2 = normalized.EnableReminder2;
            reminder2LeadTimeInMinutes = normalized.Reminder2LeadTimeInMinutes;
        }

        public IReadOnlyList<ReminderThreshold> BuildThresholds()
        {
            return ToDomainConfiguration().BuildThresholds();
        }

        public ReminderConfiguration ToDomainConfiguration()
        {
            return new ReminderConfiguration(enableReminder1, reminder1LeadTimeInMinutes, enableReminder2, reminder2LeadTimeInMinutes);
        }
    }
}
