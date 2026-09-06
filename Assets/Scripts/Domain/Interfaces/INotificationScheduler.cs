using Yobi.Domain.Entities;

namespace Yobi.Domain.Interfaces
{
    // A port over the OS-level "fire this at a future time even if the app isn't running"
    // mechanism (e.g. macOS UNUserNotificationCenter). Scheduling with an id already pending
    // must replace it, so callers can freely re-schedule on every refresh without duplicating
    // notifications. Clicking a delivered notification (opening its Url) is handled entirely
    // by the implementation - nothing above this port needs to react to it.
    public interface INotificationScheduler
    {
        void RequestAuthorization();

        void Schedule(ScheduledReminderNotification notification);

        void Cancel(string id);

        // For turning notifications off entirely (e.g. the Settings modal's toggle): the
        // reconciling caller (SyncScheduledRemindersUseCase) only tracks what it scheduled
        // in-memory during the current run, so it has no way to Cancel() by id anything left
        // over from a previous session once that in-memory state doesn't exist yet.
        void CancelAll();
    }
}
