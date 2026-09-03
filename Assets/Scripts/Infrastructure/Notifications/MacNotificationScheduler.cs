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

        private delegate void ClickCallback(string identifier, string url);

        [DllImport(PluginName)]
        private static extern void Yobi_RequestAuthorization();

        [DllImport(PluginName)]
        private static extern void Yobi_ScheduleNotification(string identifier, string title, string body, string url, double fireAtUnixTimeUtc);

        [DllImport(PluginName)]
        private static extern void Yobi_CancelNotification(string identifier);

        [DllImport(PluginName)]
        private static extern void Yobi_SetClickCallback(ClickCallback callback);

        // The Url travels with the OS-persisted notification request itself (native side stores
        // it in UNMutableNotificationContent.userInfo and hands it back on click), rather than an
        // in-process dictionary here - so a click still resolves correctly even for a notification
        // that fired while Yobi wasn't running.
        private static readonly ConcurrentQueue<string> PendingClickUrls = new ConcurrentQueue<string>();
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
            var utc = System.DateTime.SpecifyKind(notification.FireAtUtc, System.DateTimeKind.Utc);
            var fireAtUnixTimeUtc = (double)new System.DateTimeOffset(utc).ToUnixTimeSeconds();

            // Lets a far-future real schedule (hours away) be sanity-checked from the Console
            // immediately, instead of having to wait for it to actually fire to know it's right.
            var localFireTime = notification.FireAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            Debug.Log($"[MacNotificationScheduler] Scheduled '{notification.Id}' for {localFireTime} (local)\n{notification.Title}\n{notification.Body}");

            Yobi_ScheduleNotification(notification.Id, notification.Title, notification.Body, notification.Url, fireAtUnixTimeUtc);
        }

        public void Cancel(string id)
        {
            Debug.Log($"[MacNotificationScheduler] Cancelled '{id}'");
            Yobi_CancelNotification(id);
        }

        // The native click callback can arrive off Unity's main thread, and opening a URL uses
        // a Unity API that isn't safe to call from there - so it's queued here and drained from
        // a MonoBehaviour.Update() on the main thread instead of acting on it immediately.
        public static void PumpPendingClicks()
        {
            while (PendingClickUrls.TryDequeue(out var url))
            {
                if (!string.IsNullOrEmpty(url))
                {
                    UnityEngine.Application.OpenURL(url);
                }
            }
        }

        [MonoPInvokeCallback(typeof(ClickCallback))]
        private static void OnNativeClick(string identifier, string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                PendingClickUrls.Enqueue(url);
            }
        }
    }
}
