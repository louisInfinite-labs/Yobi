using System;
using System.Collections.Generic;
using Yobi.Application.Models;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Application.UseCases
{
    // Reconciles "what should be scheduled with the OS right now" against "what we last told
    // the OS to schedule", so the whole Phase 2 approach of pre-scheduling ahead of time (the
    // OS fires it even if Yobi itself isn't running) stays correct as Holodex data changes:
    // a rescheduled stream gets its notification moved, a cancelled/removed one gets its
    // notification cancelled, and an unchanged one is left alone (no redundant re-scheduling).
    public sealed class SyncScheduledRemindersUseCase
    {
        private readonly INotificationScheduler _scheduler;

        // Keyed by notification id ("{videoId}::{thresholdLabel}") -> the fire time we last
        // scheduled it for. This is intentionally in-memory only: if Yobi restarts, every
        // still-relevant notification gets recomputed and re-scheduled, which is harmless
        // because scheduling with an existing id replaces the pending OS-level request.
        private readonly Dictionary<string, DateTime> _lastKnownSchedule = new Dictionary<string, DateTime>();

        public SyncScheduledRemindersUseCase(INotificationScheduler scheduler)
        {
            _scheduler = scheduler;
        }

        public void Sync(
            IReadOnlyList<ChannelLivestreamResult> channelResults,
            IReadOnlyList<ReminderThreshold> thresholds,
            DateTime nowUtc)
        {
            var desired = BuildDesiredSchedule(channelResults, thresholds, nowUtc);

            foreach (var kvp in desired)
            {
                var id = kvp.Key;
                var notification = kvp.Value;

                if (_lastKnownSchedule.TryGetValue(id, out var previousFireAtUtc) && previousFireAtUtc == notification.FireAtUtc)
                {
                    continue;
                }

                _scheduler.Schedule(notification);
                _lastKnownSchedule[id] = notification.FireAtUtc;
            }

            var staleIds = new List<string>();
            foreach (var id in _lastKnownSchedule.Keys)
            {
                if (!desired.ContainsKey(id))
                {
                    staleIds.Add(id);
                }
            }

            foreach (var id in staleIds)
            {
                _scheduler.Cancel(id);
                _lastKnownSchedule.Remove(id);
            }
        }

        private static Dictionary<string, ScheduledReminderNotification> BuildDesiredSchedule(
            IReadOnlyList<ChannelLivestreamResult> channelResults,
            IReadOnlyList<ReminderThreshold> thresholds,
            DateTime nowUtc)
        {
            var desired = new Dictionary<string, ScheduledReminderNotification>();

            foreach (var channelResult in channelResults)
            {
                foreach (var livestream in channelResult.Livestreams)
                {
                    foreach (var threshold in thresholds)
                    {
                        var fireAtUtc = livestream.ScheduledStartUtc - threshold.LeadTime;

                        // A trigger in the past can't be scheduled with the OS, and the roadmap
                        // rule is that missed reminders are never backfilled - so it's simply
                        // left out of the desired set (and cancelled below if it was pending).
                        if (fireAtUtc <= nowUtc)
                        {
                            continue;
                        }

                        var id = BuildId(livestream.VideoId, threshold.Label);
                        var title = channelResult.Channel.Name;
                        var leadMinutes = (int)threshold.LeadTime.TotalMinutes;
                        var body = $"{livestream.Title}\n{threshold.Label}: {leadMinutes} 分鐘後開始";

                        desired[id] = new ScheduledReminderNotification(id, title, body, livestream.Url, fireAtUtc);
                    }
                }
            }

            return desired;
        }

        private static string BuildId(string videoId, string thresholdLabel)
        {
            return $"{videoId}::{thresholdLabel}";
        }
    }
}
