using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Yobi.Application.Models;
using Yobi.Application.UseCases;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;
using Yobi.Infrastructure.Api.Holodex;
using Yobi.Infrastructure.Config;
using Yobi.Infrastructure.Mock;
using Yobi.Infrastructure.Notifications;
using Yobi.Infrastructure.Storage;

namespace Yobi.Presentation
{
    public sealed class CreatorSearchPanelBehaviour : MonoBehaviour
    {
        private const float ReminderCheckIntervalSeconds = 5f;

        // Lets other UI (RoomReminderListBehaviour) show the same watchlist statuses without
        // running its own separate Holodex polling loop, which would otherwise double the
        // request rate against the same API key/rate limit for no benefit.
        public event Action<IReadOnlyList<CreatorStatus>> WatchlistStatusUpdated;

        [SerializeField]
        private Button refreshStatusButton;

        [SerializeField]
        private Text statusText;

        [SerializeField]
        private RectTransform watchlistContainer;

        [SerializeField]
        private GameObject watchlistRowTemplate;

        [SerializeField]
        private ReminderSettings reminderSettings;

        [Header("Holodex Polling")]
        [SerializeField]
        private bool enablePolling = true;

        [SerializeField]
        private int pollingIntervalInMinutes = 5;

        [Header("Data Source")]
        [SerializeField]
        private LivestreamDataSourceMode dataSource = LivestreamDataSourceMode.Real;

        [Tooltip("Mock mode only: how far from now (in minutes) the single mock creator's livestream is scheduled. <=0 means currently LIVE.")]
        [SerializeField]
        private int mockStreamStartInMinutes = 6;

        private SearchCreatorsUseCase _searchCreatorsUseCase;
        private ManageWatchlistUseCase _watchlistUseCase;
        private GetCreatorStatusUseCase _creatorStatusUseCase;
        private EvaluateLivestreamRemindersUseCase _reminderUseCase;
        private IReminderConfigurationRepository _reminderConfigurationRepository;
        private ReminderConfiguration _reminderConfiguration;
        private SyncScheduledRemindersUseCase _syncScheduledRemindersUseCase;

        private bool _isConfigured;
        private bool _isRefreshing;
        private bool _refreshPending;
        private CancellationTokenSource _pollingCts;

        // Updated only by a completed refresh; read every few seconds by ReminderCheckLoop.
        // This is what keeps reminder timing local and decoupled from how often we actually
        // poll Holodex - the reminder check never itself performs a network call.
        private IReadOnlyList<ChannelLivestreamResult> _latestReminderSnapshot = new List<ChannelLivestreamResult>();

        private readonly List<GameObject> _activeWatchlistRows = new List<GameObject>();

        private void OnValidate()
        {
            if (pollingIntervalInMinutes < 1)
            {
                pollingIntervalInMinutes = 1;
            }
        }

        private void Awake()
        {
            if (refreshStatusButton == null || watchlistContainer == null || watchlistRowTemplate == null)
            {
                Debug.LogError("[CreatorSearchPanel] Required UI references are not assigned. Run Tools > Yobi > Setup Creator Search UI.");
            }

            if (watchlistRowTemplate != null)
            {
                watchlistRowTemplate.SetActive(false);
            }

            // This panel no longer has an on-screen toggle button (its manual search UI was
            // replaced by MainSearchBarBehaviour) - nothing outside this class hides it anymore,
            // so it hides itself permanently here. Still a CanvasGroup, never SetActive(false):
            // deactivating the GameObject would stop this script's own Start()/coroutines (the
            // watchlist poll + reminder loops below) and make it invisible to other scripts'
            // FindFirstObjectByType lookups (e.g. RoomReminderListBehaviour, MainSearchBarBehaviour).
            var canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            _watchlistUseCase = new ManageWatchlistUseCase(new LocalFileWatchlistRepository());
            _reminderUseCase = new EvaluateLivestreamRemindersUseCase();

            _reminderConfigurationRepository = new LocalFileReminderConfigurationRepository();
            var defaultReminderConfiguration = reminderSettings != null
                ? reminderSettings.ToDomainConfiguration()
                : new ReminderConfiguration(enableReminder1: true, reminder1LeadTimeInMinutes: 30, enableReminder2: true, reminder2LeadTimeInMinutes: 15);
            _reminderConfiguration = _reminderConfigurationRepository.Load(defaultReminderConfiguration);

            // Pre-scheduled OS notifications (Yobi_* native calls) only exist for macOS - other
            // platforms simply keep the console-only reminder path until Phase 4 cross-platform work.
            if (UnityEngine.Application.platform == RuntimePlatform.OSXPlayer || UnityEngine.Application.platform == RuntimePlatform.OSXEditor)
            {
                var scheduler = new MacNotificationScheduler();
                scheduler.RequestAuthorization();
                _syncScheduledRemindersUseCase = new SyncScheduledRemindersUseCase(scheduler);
            }

            try
            {
                ICreatorSearchProvider searchProvider;
                ICreatorLivestreamStatusProvider statusProvider;

                if (dataSource == LivestreamDataSourceMode.Mock)
                {
                    // Zero network calls, zero dependency on config.local.json - purely local
                    // so LIVE/UPCOMING/NONE and reminder behaviour can be verified without
                    // touching Holodex at all.
                    searchProvider = new MockCreatorSearchProvider();
                    statusProvider = new MockCreatorLivestreamStatusProvider(TimeSpan.FromMinutes(mockStreamStartInMinutes));
                }
                else
                {
                    var configProvider = new LocalFileChannelConfigProvider();
                    var holodexClient = new HolodexApiClient(configProvider.GetHolodexApiKey());
                    searchProvider = holodexClient;
                    statusProvider = holodexClient;
                }

                _searchCreatorsUseCase = new SearchCreatorsUseCase(searchProvider);
                _creatorStatusUseCase = new GetCreatorStatusUseCase(statusProvider);
                _isConfigured = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CreatorSearchPanel] Failed to load Holodex configuration: {ex.Message}");
                SetStatus("Search unavailable: configuration error.");
                return;
            }

            if (refreshStatusButton != null)
            {
                refreshStatusButton.onClick.AddListener(OnRefreshButtonClicked);
            }
        }

        private void Start()
        {
            // Reminder checking runs on its own frequent, purely-local cadence regardless of
            // whether polling is enabled or how coarse its interval is.
            StartCoroutine(ReminderCheckLoop());

            if (_isConfigured && enablePolling)
            {
                _pollingCts = new CancellationTokenSource();
                RunPollingLoop(_pollingCts.Token);
            }
        }

        private void Update()
        {
            // Native notification clicks can arrive off the main thread; draining here is what
            // makes it safe to call Application.OpenURL in response to one.
            if (_syncScheduledRemindersUseCase != null)
            {
                MacNotificationScheduler.PumpPendingClicks();
            }
        }

        private void OnDestroy()
        {
            _pollingCts?.Cancel();
            _pollingCts?.Dispose();
        }

        // App start -> immediate refresh -> wait configured interval -> refresh again -> repeat.
        private async void RunPollingLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await RefreshWatchlistStatusAsync(cancellationToken);

                    var intervalMinutes = Mathf.Max(1, pollingIntervalInMinutes);
                    await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the panel is destroyed/torn down mid-wait.
            }
        }

        // Entry point for MainSearchBarBehaviour's search-first intent routing. Exposed here
        // (rather than letting the bar construct its own SearchCreatorsUseCase) purely for
        // symmetry with AddToWatchlist below - this one has no shared-state hazard of its own,
        // but keeping both entry points on the same class keeps the "who owns Holodex access"
        // story in one place.
        public Task<IReadOnlyList<CreatorSearchResult>> SearchCreatorsAsync(string query, CancellationToken cancellationToken)
        {
            return _searchCreatorsUseCase.SearchAsync(query, cancellationToken);
        }

        // Entry point for MainSearchBarBehaviour's add-to-watchlist action. Routed through this
        // instance specifically (rather than the bar constructing its own ManageWatchlistUseCase)
        // because _watchlistUseCase is loaded once into memory and never reloaded from disk - a
        // second independent instance would leave RunPollingLoop's in-memory copy unaware of
        // anything added here until the next app restart, silently breaking reminders for it.
        public WatchlistAddResult AddToWatchlist(CreatorSearchResult result)
        {
            var addResult = _watchlistUseCase.Add(result.ChannelId, result.DisplayName, result.ChannelUrl);
            if (addResult == WatchlistAddResult.Added)
            {
                _ = RefreshWatchlistStatusAsync();
            }

            return addResult;
        }

        private async void OnRefreshButtonClicked()
        {
            await RefreshWatchlistStatusAsync();
        }

        private async Task RefreshWatchlistStatusAsync(CancellationToken cancellationToken = default)
        {
            if (_isRefreshing)
            {
                // A refresh (poll tick, manual click, or post-Add) is already in flight. Rather
                // than silently dropping this request - which would leave a creator added mid-poll
                // invisible until the next poll interval - flag that fresh data is wanted and let
                // the in-flight refresh below pick it up as an immediate follow-up pass once it's
                // done, instead of running concurrently.
                _refreshPending = true;
                return;
            }

            _isRefreshing = true;
            SetRefreshInteractable(false);

            try
            {
                do
                {
                    _refreshPending = false;

                    var watched = _watchlistUseCase.GetAll();
                    var identities = new List<ChannelIdentity>(watched.Count);
                    foreach (var creator in watched)
                    {
                        identities.Add(new ChannelIdentity(creator.ChannelId, creator.DisplayName));
                    }

                    var statuses = await _creatorStatusUseCase.GetStatusesAsync(identities, isWatchlisted: true, cancellationToken);

                    RenderWatchlistRows(statuses);
                    WatchlistStatusUpdated?.Invoke(statuses);
                    _latestReminderSnapshot = BuildChannelLivestreamResults(statuses);

                    _syncScheduledRemindersUseCase?.Sync(_latestReminderSnapshot, _reminderConfiguration.BuildThresholds(), DateTime.UtcNow);
                }
                while (_refreshPending);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Holodex] Failed to refresh watchlist status: {ex.Message}");
            }
            finally
            {
                _isRefreshing = false;
                SetRefreshInteractable(true);
            }
        }

        private void SetRefreshInteractable(bool interactable)
        {
            if (refreshStatusButton != null)
            {
                refreshStatusButton.interactable = interactable;
            }
        }

        private void RenderWatchlistRows(IReadOnlyList<CreatorStatus> statuses)
        {
            ClearWatchlistRows();

            if (watchlistRowTemplate == null || watchlistContainer == null)
            {
                return;
            }

            foreach (var status in statuses)
            {
                var row = Instantiate(watchlistRowTemplate, watchlistContainer);
                row.SetActive(true);

                var nameText = row.transform.Find("NameText")?.GetComponent<Text>();
                if (nameText != null)
                {
                    nameText.text = FormatCreatorStatus(status);
                }

                _activeWatchlistRows.Add(row);
                LogCreatorStatus(status);
            }
        }

        private void LogCreatorStatus(CreatorStatus status)
        {
            var tagPrefix = dataSource == LivestreamDataSourceMode.Mock ? "Mock" : "Holodex";
            var statusTag = status.LiveStatus.ToString().ToUpperInvariant();

            if (status.LiveStatus == CreatorLiveStatus.Live && status.CurrentLivestream != null)
            {
                Debug.Log($"[{tagPrefix}][{statusTag}]\nChannel: {status.ChannelName}\nTitle: {status.CurrentLivestream.Title}\nVideo ID: {status.CurrentLivestream.VideoId}\nURL: {status.CurrentLivestream.Url}");
            }
            else if (status.LiveStatus == CreatorLiveStatus.Upcoming)
            {
                foreach (var upcoming in status.UpcomingLivestreams)
                {
                    var localStart = upcoming.ScheduledStartUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                    Debug.Log($"[{tagPrefix}][{statusTag}]\nChannel: {status.ChannelName}\nTitle: {upcoming.Title}\nScheduled Start: {localStart}\nVideo ID: {upcoming.VideoId}\nURL: {upcoming.Url}");
                }
            }
            else
            {
                Debug.Log($"[{tagPrefix}][{statusTag}]\nChannel: {status.ChannelName}\nNo live or upcoming livestreams within the next 24 hours.");
            }
        }

        // Shared by both Search Results (single, not-yet-added creator) and the Watchlist
        // (bulk, already-added creators) so the display format never diverges between paths.
        private static string FormatCreatorStatus(CreatorStatus status)
        {
            var lines = new List<string>
            {
                status.ChannelName,
                status.ChannelUrl,
                $"Watchlisted: {(status.IsWatchlisted ? "Yes" : "No")}",
                $"Status: {status.LiveStatus.ToString().ToUpperInvariant()}",
            };

            // Only known when the same livestream call happened to return it (i.e. there was at
            // least one live/upcoming video); not chased down with an extra API call otherwise.
            if (!string.IsNullOrEmpty(status.Studio))
            {
                lines.Add($"Studio: {status.Studio}");
            }

            if (status.LiveStatus == CreatorLiveStatus.Live && status.CurrentLivestream != null)
            {
                lines.Add($"Live: {status.CurrentLivestream.Title}");
                lines.Add($"URL: {status.CurrentLivestream.Url}");
            }
            else if (status.LiveStatus == CreatorLiveStatus.Upcoming)
            {
                foreach (var upcoming in status.UpcomingLivestreams)
                {
                    var localStart = upcoming.ScheduledStartUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                    lines.Add($"Upcoming: {upcoming.Title} @ {localStart}");
                    lines.Add($"URL: {upcoming.Url}");
                }
            }

            return string.Join("\n", lines);
        }

        private static List<ChannelLivestreamResult> BuildChannelLivestreamResults(IReadOnlyList<CreatorStatus> statuses)
        {
            var channelResults = new List<ChannelLivestreamResult>();
            foreach (var status in statuses)
            {
                if (status.UpcomingLivestreams.Count == 0)
                {
                    continue;
                }

                var identity = new ChannelIdentity(status.ChannelId, status.ChannelName);
                channelResults.Add(new ChannelLivestreamResult(identity, status.UpcomingLivestreams));
            }

            return channelResults;
        }

        // Ticks locally every few seconds, independent of the (much coarser) Holodex polling
        // interval - it only ever compares "now" against the most recently fetched snapshot,
        // never makes a network call itself.
        private IEnumerator ReminderCheckLoop()
        {
            while (true)
            {
                EvaluateReminders();
                yield return new WaitForSeconds(ReminderCheckIntervalSeconds);
            }
        }

        private void EvaluateReminders()
        {
            if (_reminderConfiguration == null || _latestReminderSnapshot.Count == 0)
            {
                return;
            }

            var thresholds = _reminderConfiguration.BuildThresholds();
            if (thresholds.Count == 0)
            {
                return;
            }

            var dueEvents = _reminderUseCase.Evaluate(_latestReminderSnapshot, thresholds, DateTime.UtcNow);
            foreach (var reminderEvent in dueEvents)
            {
                LogReminder(reminderEvent);
            }
        }

        private static void LogReminder(LivestreamReminderEvent reminderEvent)
        {
            var localStart = reminderEvent.ScheduledStartUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            var leadMinutes = (int)reminderEvent.Threshold.LeadTime.TotalMinutes;
            Debug.Log($"[Reminder]\nChannel: {reminderEvent.ChannelName}\nTitle: {reminderEvent.Title}\n{reminderEvent.Threshold.Label}: {leadMinutes} minutes before start\nScheduled Start: {localStart}\nVideo ID: {reminderEvent.VideoId}");
        }

        private void ClearWatchlistRows()
        {
            foreach (var row in _activeWatchlistRows)
            {
                Destroy(row);
            }

            _activeWatchlistRows.Clear();
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }
    }
}
