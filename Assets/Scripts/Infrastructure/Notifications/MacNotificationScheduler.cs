using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Infrastructure.Notifications
{
    // Bridges to Assets/Plugins/macOS/YobiNotifications.bundle (source: Native/YobiNotifications.m),
    // which wraps UNUserNotificationCenter. macOS only - callers must not construct this on other
    // platforms, since the native library won't be present to load.
    public sealed class MacNotificationScheduler : INotificationScheduler
    {
        private const string PluginName = "YobiNotifications";

        private delegate void ClickCallback(string identifier);

        [DllImport(PluginName)]
        private static extern void Yobi_RequestAuthorization();

        [DllImport(PluginName)]
        private static extern void Yobi_ScheduleNotification(string identifier, string title, string body, double fireAtUnixTimeUtc);

        [DllImport(PluginName)]
        private static extern void Yobi_CancelNotification(string identifier);

        [DllImport(PluginName)]
        private static extern void Yobi_SetClickCallback(ClickCallback callback);

        // Native only reports back the identifier that was clicked; the Url to open for that
        // id is known only here (whatever it was scheduled with most recently), so the whole
        // "click -> open stream" behaviour stays inside this adapter.
        private static readonly ConcurrentDictionary<string, string> UrlsById = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentQueue<string> PendingClickIds = new ConcurrentQueue<string>();
        private static bool _callbackRegistered;

        public MacNotificationScheduler()
        {
            if (!_callbackRegistered)
            {
                Yobi_SetClickCallback(OnNativeClick);
                _callbackRegistered = true;
            }
        }

        public void RequestAuthorization()
        {
            Yobi_RequestAuthorization();
        }

        public void Schedule(ScheduledReminderNotification notification)
        {
            UrlsById[notification.Id] = notification.Url;

            var utc = System.DateTime.SpecifyKind(notification.FireAtUtc, System.DateTimeKind.Utc);
            var fireAtUnixTimeUtc = (double)new System.DateTimeOffset(utc).ToUnixTimeSeconds();

            // Lets a far-future real schedule (hours away) be sanity-checked from the Console
            // immediately, instead of having to wait for it to actually fire to know it's right.
            var localFireTime = notification.FireAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            Debug.Log($"[MacNotificationScheduler] Scheduled '{notification.Id}' for {localFireTime} (local)\n{notification.Title}\n{notification.Body}");

            Yobi_ScheduleNotification(notification.Id, notification.Title, notification.Body, fireAtUnixTimeUtc);
        }

        public void Cancel(string id)
        {
            UrlsById.TryRemove(id, out _);
            Debug.Log($"[MacNotificationScheduler] Cancelled '{id}'");
            Yobi_CancelNotification(id);
        }

        // The native click callback can arrive off Unity's main thread, and opening a URL uses
        // a Unity API that isn't safe to call from there - so it's queued here and drained from
        // a MonoBehaviour.Update() on the main thread instead of acting on it immediately.
        public static void PumpPendingClicks()
        {
            while (PendingClickIds.TryDequeue(out var id))
            {
                if (UrlsById.TryGetValue(id, out var url) && !string.IsNullOrEmpty(url))
                {
                    UnityEngine.Application.OpenURL(url);
                }
            }
        }

        [MonoPInvokeCallback(typeof(ClickCallback))]
        private static void OnNativeClick(string identifier)
        {
            if (!string.IsNullOrEmpty(identifier))
            {
                PendingClickIds.Enqueue(identifier);
            }
        }
    }
}
