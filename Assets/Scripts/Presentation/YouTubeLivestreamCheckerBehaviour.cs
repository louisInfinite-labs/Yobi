using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Yobi.Application.Models;
using Yobi.Application.UseCases;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;
using Yobi.Infrastructure.Api.Holodex;
using Yobi.Infrastructure.Config;
using Yobi.Infrastructure.Mock;
using Yobi.Infrastructure.YouTube;

namespace Yobi.Presentation
{
    public sealed class YouTubeLivestreamCheckerBehaviour : MonoBehaviour
    {
        private const float ReminderCheckIntervalSeconds = 5f;

        [SerializeField]
        private bool runOnStart = false;

        [SerializeField]
        private LivestreamDataSourceMode dataSource = LivestreamDataSourceMode.Real;

        [SerializeField]
        private bool enableReminder1 = true;

        [SerializeField]
        private int reminder1LeadTimeInMinutes = 30;

        [SerializeField]
        private bool enableReminder2 = true;

        [SerializeField]
        private int reminder2LeadTimeInMinutes = 15;

        [SerializeField]
        private int mockStreamStartInMinutes = 6;

        [Header("Temporary Debug: Holodex Connectivity")]
        [Tooltip("Debug-only connectivity check. Remove once Holodex becomes a selectable Data Source alongside Real/Mock.")]
        [SerializeField]
        private bool testHolodexConnectionOnStart = false;

        private EvaluateLivestreamRemindersUseCase _reminderUseCase;

        private void OnValidate()
        {
            NormalizeReminderSettings();
        }

        private async void Start()
        {
            if (runOnStart)
            {
                await RunCheckAsync();
            }
            else
            {
                Debug.Log("[YouTube] Auto check is disabled.");
            }

            if (testHolodexConnectionOnStart)
            {
                await RunHolodexConnectionTestAsync();
            }
        }

        private async System.Threading.Tasks.Task RunHolodexConnectionTestAsync()
        {
            try
            {
                var configProvider = new LocalFileChannelConfigProvider();
                var holodexClient = new HolodexApiClient(configProvider.GetHolodexApiKey());
                var result = await holodexClient.TestConnectionAsync(CancellationToken.None);

                if (result.IsSuccess)
                {
                    Debug.Log($"[Holodex] Connection successful. Received {result.ItemCount} item(s).");
                }
                else
                {
                    Debug.LogError($"[Holodex] Connection failed: {result.ErrorMessage}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Holodex] Connection test failed: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task RunCheckAsync()
        {
            try
            {
                var beforeReminder1 = reminder1LeadTimeInMinutes;
                var beforeReminder2 = reminder2LeadTimeInMinutes;
                NormalizeReminderSettings();
                if (beforeReminder1 != reminder1LeadTimeInMinutes || beforeReminder2 != reminder2LeadTimeInMinutes)
                {
                    Debug.LogWarning($"[YouTube] Reminder settings were invalid and have been corrected. Reminder 1 = {reminder1LeadTimeInMinutes} min, Reminder 2 = {reminder2LeadTimeInMinutes} min.");
                }

                var configProvider = new LocalFileChannelConfigProvider();
                IYouTubeLivestreamRepository repository = dataSource == LivestreamDataSourceMode.Mock
                    ? new MockYouTubeLivestreamRepository(TimeSpan.FromMinutes(mockStreamStartInMinutes))
                    : new YouTubeDataApiLivestreamRepository(configProvider.GetApiKey());

                var useCase = new CheckUpcomingLivestreamsUseCase(configProvider, repository);
                var results = await useCase.ExecuteAsync(CancellationToken.None);

                foreach (var result in results)
                {
                    if (result.Livestreams.Count == 0)
                    {
                        Debug.Log($"[YouTube]\nChannel: {result.Channel.Name}\nNo upcoming livestreams within the next 24 hours.");
                        continue;
                    }

                    foreach (var livestream in result.Livestreams)
                    {
                        var localStart = livestream.ScheduledStartUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                        Debug.Log($"[YouTube]\nChannel: {result.Channel.Name}\nTitle: {livestream.Title}\nScheduled Start: {localStart}\nVideo ID: {livestream.VideoId}\nURL: {livestream.Url}");
                    }
                }

                var thresholds = BuildReminderThresholds();
                if (thresholds.Count > 0)
                {
                    _reminderUseCase = new EvaluateLivestreamRemindersUseCase();
                    StartCoroutine(ReminderCheckLoop(results, thresholds));
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[YouTube] Failed to check upcoming livestreams: {ex.Message}");
            }
        }

        private List<ReminderThreshold> BuildReminderThresholds()
        {
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

        private void NormalizeReminderSettings()
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

        private IEnumerator ReminderCheckLoop(IReadOnlyList<ChannelLivestreamResult> snapshot, IReadOnlyList<ReminderThreshold> thresholds)
        {
            while (true)
            {
                var dueEvents = _reminderUseCase.Evaluate(snapshot, thresholds, DateTime.UtcNow);
                foreach (var reminderEvent in dueEvents)
                {
                    LogReminder(reminderEvent);
                }

                if (AllLivestreamsStarted(snapshot, DateTime.UtcNow))
                {
                    yield break;
                }

                yield return new WaitForSeconds(ReminderCheckIntervalSeconds);
            }
        }

        private static bool AllLivestreamsStarted(IReadOnlyList<ChannelLivestreamResult> snapshot, DateTime nowUtc)
        {
            foreach (var result in snapshot)
            {
                foreach (var livestream in result.Livestreams)
                {
                    if (livestream.ScheduledStartUtc > nowUtc)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void LogReminder(LivestreamReminderEvent reminderEvent)
        {
            var localStart = reminderEvent.ScheduledStartUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            var leadMinutes = (int)reminderEvent.Threshold.LeadTime.TotalMinutes;
            Debug.Log($"[Reminder]\nChannel: {reminderEvent.ChannelName}\nTitle: {reminderEvent.Title}\n{reminderEvent.Threshold.Label}: {leadMinutes} minutes before start\nScheduled Start: {localStart}\nVideo ID: {reminderEvent.VideoId}");
        }
    }
}
