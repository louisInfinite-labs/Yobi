using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Yobi.Application.Models;
using Yobi.Domain.Entities;

namespace Yobi.Presentation
{
    // Collapsible list of watchlisted creators, tucked behind a "<mode> ▼" header (collapsed by
    // default) so it isn't a permanent block of screen real estate the user isn't currently
    // looking at - click the header to expand/collapse. A second small icon button in that same
    // header (switchModeButton, the swap_horiz glyph) flips between two modes without needing two
    // separate always-visible sections: "Live Status" surfaces only who's live/scheduled soon,
    // "Follow List" is the full watchlist regardless of status. Fed by CreatorSearchPanelBehaviour's
    // own watchlist refresh - deliberately doesn't run its own polling loop, to avoid doubling the
    // Holodex request rate.
    public sealed class RoomReminderListBehaviour : MonoBehaviour
    {
        private const string LiveStatusModeLabel = "Live Status";
        private const string FollowListModeLabel = "Follow List";

        [SerializeField]
        private Button headerButton;

        [SerializeField]
        private Text headerLabel;

        [SerializeField]
        private Button switchModeButton;

        [SerializeField]
        private GameObject contentPanel;

        [SerializeField]
        private RectTransform rowContainer;

        [SerializeField]
        private GameObject rowTemplate;

        private readonly List<GameObject> _activeRows = new List<GameObject>();
        private CreatorSearchPanelBehaviour _searchPanel;
        private bool _showFollowList;

        // Statuses arrive only on CreatorSearchPanelBehaviour's own refresh cadence (polling
        // interval or a manual add) - switching modes needs to re-render immediately against
        // whatever was last received, rather than waiting for the next refresh to happen to land.
        private IReadOnlyList<CreatorStatus> _latestStatuses = Array.Empty<CreatorStatus>();

        private string CurrentModeLabel => _showFollowList ? FollowListModeLabel : LiveStatusModeLabel;
        private string CollapsedLabel => $"{CurrentModeLabel} ▼";
        private string ExpandedLabel => $"{CurrentModeLabel} ▲";

        private void Start()
        {
            if (rowTemplate != null)
            {
                rowTemplate.SetActive(false);
            }

            // Collapsed by default - SetActive(false) here (rather than only relying on the
            // Editor tool's own initial state) is what keeps it collapsed on every subsequent
            // rebuild too, since the Editor tool only sets this up once for a brand-new panel.
            if (contentPanel != null)
            {
                contentPanel.SetActive(false);
            }

            if (headerLabel != null)
            {
                headerLabel.text = CollapsedLabel;
            }

            if (headerButton != null)
            {
                headerButton.onClick.AddListener(ToggleExpanded);
            }

            if (switchModeButton != null)
            {
                switchModeButton.onClick.AddListener(OnSwitchModeButtonClicked);
            }

            _searchPanel = FindFirstObjectByType<CreatorSearchPanelBehaviour>();
            if (_searchPanel != null)
            {
                _searchPanel.WatchlistStatusUpdated += OnWatchlistStatusUpdated;
            }
        }

        private void ToggleExpanded()
        {
            if (contentPanel == null)
            {
                return;
            }

            var expanded = !contentPanel.activeSelf;
            contentPanel.SetActive(expanded);

            if (headerLabel != null)
            {
                headerLabel.text = expanded ? ExpandedLabel : CollapsedLabel;
            }
        }

        private void OnSwitchModeButtonClicked()
        {
            _showFollowList = !_showFollowList;

            if (headerLabel != null)
            {
                headerLabel.text = contentPanel != null && contentPanel.activeSelf ? ExpandedLabel : CollapsedLabel;
            }

            RenderRows(_latestStatuses);
        }

        private void OnDestroy()
        {
            if (_searchPanel != null)
            {
                _searchPanel.WatchlistStatusUpdated -= OnWatchlistStatusUpdated;
            }
        }

        private void OnWatchlistStatusUpdated(IReadOnlyList<CreatorStatus> statuses)
        {
            _latestStatuses = statuses;
            RenderRows(statuses);
        }

        private void RenderRows(IReadOnlyList<CreatorStatus> statuses)
        {
            ClearRows();

            if (rowTemplate == null || rowContainer == null)
            {
                return;
            }

            foreach (var status in statuses)
            {
                // Live Status mode only wants creators worth surfacing right now - live or
                // scheduled soon; a watched creator with nothing upcoming would just be dead
                // weight in that always-visible list. Follow List mode shows the full watchlist
                // regardless.
                if (!_showFollowList && status.LiveStatus == CreatorLiveStatus.None)
                {
                    continue;
                }

                var row = Instantiate(rowTemplate, rowContainer);
                row.SetActive(true);

                var nameText = row.transform.Find("NameText")?.GetComponent<Text>();
                if (nameText != null)
                {
                    nameText.text = CleanDisplayName(status.ChannelName);
                }

                var statusText = row.transform.Find("StatusText")?.GetComponent<Text>();
                if (statusText != null)
                {
                    statusText.text = DescribeStatus(status);
                }

                var dot = row.transform.Find("Dot")?.GetComponent<Image>();
                if (dot != null)
                {
                    // Red = live now, gray = scheduled, dim gray = not live/scheduled (only ever
                    // reached in Follow List mode, since Live Status mode filters None out above).
                    dot.color = status.LiveStatus == CreatorLiveStatus.Live
                        ? new Color(0.86f, 0.15f, 0.15f)
                        : status.LiveStatus == CreatorLiveStatus.Upcoming
                            ? new Color(0.6f, 0.6f, 0.6f)
                            : new Color(0.35f, 0.35f, 0.35f);
                }

                // Follow List only - unfollowing from Live Status (a filtered, temporary view of
                // the same data) would be an odd place to lose a creator from the watchlist
                // entirely, so the "✕" only shows in Follow List mode where it's clearly "this is
                // the whole watchlist".
                var removeButton = row.transform.Find("RemoveButton")?.GetComponent<Button>();
                if (removeButton != null)
                {
                    removeButton.gameObject.SetActive(_showFollowList);
                    var channelId = status.ChannelId;
                    removeButton.onClick.AddListener(() => _searchPanel?.RemoveFromWatchlist(channelId));
                }

                _activeRows.Add(row);
            }
        }

        // Holodex channel names mix a Latin "Ch." branding prefix, the localized (JP) name, and
        // often a trailing "- <group>" / "/ <romanization>" suffix, in inconsistent order and
        // punctuation across channels (e.g. "Hajime Ch. 轟はじめ ‐ ReGLOSS" vs "アキロゼCh。Vtuber
        // /ホロライブ所属") - there's no reliable way to isolate "just the Japanese name" from that
        // with pure string parsing, since which side of "Ch." the actual name lands on varies per
        // channel. This instead trims the most common noise (everything from the first separator
        // onward), which is what actually shortens the visibly long names in this narrow list.
        private static readonly string[] NameSeparators = { " - ", " ‐ ", "-", "‐", "/", "／" };

        private static string CleanDisplayName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName))
            {
                return rawName;
            }

            var earliestIndex = -1;
            foreach (var separator in NameSeparators)
            {
                var index = rawName.IndexOf(separator, StringComparison.Ordinal);
                if (index > 0 && (earliestIndex == -1 || index < earliestIndex))
                {
                    earliestIndex = index;
                }
            }

            var trimmed = earliestIndex > 0 ? rawName.Substring(0, earliestIndex).Trim() : rawName.Trim();
            return string.IsNullOrEmpty(trimmed) ? rawName : trimmed;
        }

        private static string DescribeStatus(CreatorStatus status)
        {
            if (status.LiveStatus == CreatorLiveStatus.Live)
            {
                return "LIVE";
            }

            if (status.LiveStatus == CreatorLiveStatus.Upcoming && status.UpcomingLivestreams.Count > 0)
            {
                return status.UpcomingLivestreams[0].ScheduledStartUtc.ToLocalTime().ToString("HH:mm");
            }

            return string.Empty;
        }

        private void ClearRows()
        {
            foreach (var row in _activeRows)
            {
                Destroy(row);
            }

            _activeRows.Clear();
        }
    }
}
