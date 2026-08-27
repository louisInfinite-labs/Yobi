using System;
using System.Collections.Generic;

namespace Yobi.Domain.Entities
{
    public sealed class LivestreamReminderState
    {
        public string VideoId { get; }

        private readonly HashSet<ReminderThreshold> _firedThresholds = new HashSet<ReminderThreshold>();
        private bool _initialized;

        public LivestreamReminderState(string videoId)
        {
            VideoId = videoId;
        }

        public IReadOnlyList<ReminderThreshold> Evaluate(DateTime nowUtc, DateTime scheduledStartUtc, IReadOnlyList<ReminderThreshold> thresholds)
        {
            var due = new List<ReminderThreshold>();
            var isFirstEvaluation = !_initialized;
            _initialized = true;

            foreach (var threshold in thresholds)
            {
                if (_firedThresholds.Contains(threshold))
                {
                    continue;
                }

                var triggerTimeUtc = scheduledStartUtc - threshold.LeadTime;
                if (nowUtc < triggerTimeUtc)
                {
                    continue;
                }

                _firedThresholds.Add(threshold);

                if (!isFirstEvaluation)
                {
                    due.Add(threshold);
                }
            }

            return due;
        }
    }
}
