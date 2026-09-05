using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Yobi.Application.Models;
using Yobi.Domain.Entities;

namespace Yobi.Presentation
{
    // Compact, always-visible list of watchlisted creators who are live now or scheduled soon,
    // so the user isn't solely reliant on the macOS notification banner (which can be dismissed
    // by accident). Fed by CreatorSearchPanelBehaviour's own watchlist refresh - deliberately
    // doesn't run its own polling loop, to avoid doubling the Holodex request rate.
    public sealed class RoomReminderListBehaviour : MonoBehaviour
    {
        [SerializeField]
        private RectTransform rowContainer;

        [SerializeField]
        private GameObject rowTemplate;

        private readonly List<GameObject> _activeRows = new List<GameObject>();
        private CreatorSearchPanelBehaviour _searchPanel;

        private void Start()
        {
            if (rowTemplate != null)
            {
                rowTemplate.SetActive(false);
            }

            _searchPanel = FindFirstObjectByType<CreatorSearchPanelBehaviour>();
            if (_searchPanel != null)
            {
                _searchPanel.WatchlistStatusUpdated += OnWatchlistStatusUpdated;
            }
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
            ClearRows();

            if (rowTemplate == null || rowContainer == null)
            {
                return;
            }

            foreach (var status in statuses)
            {
                // Only creators worth surfacing right now - live or scheduled soon. A watched
                // creator with nothing upcoming would just be dead weight in an always-visible
                // list.
                if (status.LiveStatus == CreatorLiveStatus.None)
                {
                    continue;
                }

                var row = Instantiate(rowTemplate, rowContainer);
                row.SetActive(true);

                var nameText = row.transform.Find("NameText")?.GetComponent<Text>();
                if (nameText != null)
                {
                    nameText.text = status.ChannelName;
                }

                var statusText = row.transform.Find("StatusText")?.GetComponent<Text>();
                if (statusText != null)
                {
                    statusText.text = DescribeStatus(status);
                }

                var dot = row.transform.Find("Dot")?.GetComponent<Image>();
                if (dot != null)
                {
                    // Red = live now, gray = scheduled - matches the reminder list mockup.
                    dot.color = status.LiveStatus == CreatorLiveStatus.Live
                        ? new Color(0.86f, 0.15f, 0.15f)
                        : new Color(0.6f, 0.6f, 0.6f);
                }

                _activeRows.Add(row);
            }
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
