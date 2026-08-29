using System;
using System.Collections.Generic;
using UnityEngine;
using Yobi.Domain.Entities;

namespace Yobi.Presentation
{
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
            Normalize();
        }

        public IReadOnlyList<ReminderThreshold> BuildThresholds()
        {
            Normalize();

            var thresholds = new List<ReminderThreshold>();

            if (enableReminder1)
            {
                thresholds.Add(new ReminderThreshold("Reminder 1", TimeSpan.FromMinutes(reminder1LeadTimeInMinutes)));
            }

            if (enableReminder2)
            {
                thresholds.Add(new ReminderThreshold("Reminder 2", TimeSpan.FromMinutes(reminder2LeadTimeInMinutes)));
            }

            return thresholds;
        }

        private void Normalize()
        {
            if (reminder1LeadTimeInMinutes < 0)
            {
                reminder1LeadTimeInMinutes = 0;
            }

            if (reminder2LeadTimeInMinutes < 0)
            {
                reminder2LeadTimeInMinutes = 0;
            }

            if (enableReminder1 && enableReminder2 && reminder1LeadTimeInMinutes == reminder2LeadTimeInMinutes)
            {
                reminder2LeadTimeInMinutes = reminder1LeadTimeInMinutes > 0
                    ? reminder1LeadTimeInMinutes - 1
                    : reminder1LeadTimeInMinutes + 1;
            }
        }
    }
}
