using System;
using UnityEngine;
using UnityEngine.UI;

namespace Yobi.Presentation
{
    // Simple digital clock for the Room UI panel - shows current local time and date, matching
    // the corner clock in the Desktop Mate-style reference layout.
    public sealed class RoomClockBehaviour : MonoBehaviour
    {
        private const float RefreshIntervalSeconds = 1f;

        [SerializeField]
        private Text timeText;

        [SerializeField]
        private Text dateText;

        private float _timeSinceLastRefresh;

        private void Awake()
        {
            Refresh();
        }

        private void Update()
        {
            _timeSinceLastRefresh += Time.unscaledDeltaTime;
            if (_timeSinceLastRefresh < RefreshIntervalSeconds)
            {
                return;
            }

            _timeSinceLastRefresh = 0f;
            Refresh();
        }

        private void Refresh()
        {
            var now = DateTime.Now;

            if (timeText != null)
            {
                timeText.text = now.ToString("HH:mm");
            }

            if (dateText != null)
            {
                dateText.text = now.ToString("ddd, MMM d");
            }
        }
    }
}
