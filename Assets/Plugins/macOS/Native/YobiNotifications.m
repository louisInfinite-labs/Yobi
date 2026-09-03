// Native macOS bridge for Yobi's pre-scheduled reminder notifications.
//
// Scheduling uses UNTimeIntervalNotificationTrigger (seconds-from-now) rather than a calendar
// trigger, so the C# side only ever has to hand over a Unix timestamp - no date-component /
// timezone conversion needs to happen on either side of the P/Invoke boundary.
//
// Rebuild with: Assets/Plugins/macOS/Native/build.sh

#import <Foundation/Foundation.h>
#import <UserNotifications/UserNotifications.h>

typedef void (*YobiNotificationClickCallback)(const char *identifier, const char *url);

static YobiNotificationClickCallback g_clickCallback = NULL;

@interface YobiNotificationDelegate : NSObject <UNUserNotificationCenterDelegate>
@end

@implementation YobiNotificationDelegate

- (void)userNotificationCenter:(UNUserNotificationCenter *)center
        willPresentNotification:(UNNotification *)notification
          withCompletionHandler:(void (^)(UNNotificationPresentationOptions options))completionHandler
{
    // Yobi's own window usually isn't focused when a reminder fires, but still show the
    // banner/sound even if it happens to be - the whole point of Phase 2 is a system-level
    // notification, not something only visible when the app happens to be frontmost.
    completionHandler(UNNotificationPresentationOptionBanner | UNNotificationPresentationOptionSound);
}

- (void)userNotificationCenter:(UNUserNotificationCenter *)center
 didReceiveNotificationResponse:(UNNotificationResponse *)response
          withCompletionHandler:(void (^)(void))completionHandler
{
    NSString *identifier = response.notification.request.identifier;
    // The URL travels with the OS-persisted notification request itself (userInfo), not just
    // in-process state - so a click still resolves correctly even if Yobi wasn't running when
    // the notification fired and got re-launched only by the click.
    NSString *url = response.notification.request.content.userInfo[@"url"];
    if (g_clickCallback != NULL && identifier != nil) {
        const char *idCopy = strdup([identifier UTF8String]);
        const char *urlCopy = strdup(url ? [url UTF8String] : "");
        YobiNotificationClickCallback callback = g_clickCallback;
        dispatch_async(dispatch_get_main_queue(), ^{
            callback(idCopy, urlCopy);
            free((void *)idCopy);
            free((void *)urlCopy);
        });
    }

    completionHandler();
}

@end

static YobiNotificationDelegate *g_delegate = nil;

static void Yobi_EnsureDelegate(void)
{
    if (g_delegate == nil) {
        g_delegate = [[YobiNotificationDelegate alloc] init];
        [UNUserNotificationCenter currentNotificationCenter].delegate = g_delegate;
    }
}

void Yobi_SetClickCallback(YobiNotificationClickCallback callback)
{
    g_clickCallback = callback;
}

void Yobi_RequestAuthorization(void)
{
    Yobi_EnsureDelegate();

    UNUserNotificationCenter *center = [UNUserNotificationCenter currentNotificationCenter];
    [center requestAuthorizationWithOptions:(UNAuthorizationOptionAlert | UNAuthorizationOptionSound)
                           completionHandler:^(BOOL granted, NSError *_Nullable error) {
        // Best-effort: Yobi's reminder logic never depends on notifications actually being
        // delivered (the in-app/console reminder path keeps working regardless), so a denied
        // or errored authorization is not surfaced back across the P/Invoke boundary.
    }];
}

void Yobi_ScheduleNotification(const char *identifier, const char *title, const char *body, const char *url, double fireAtUnixTimeUtc)
{
    Yobi_EnsureDelegate();

    NSDate *fireDate = [NSDate dateWithTimeIntervalSince1970:fireAtUnixTimeUtc];
    NSTimeInterval interval = [fireDate timeIntervalSinceNow];
    if (interval <= 0) {
        // The C# side already filters out past-due triggers before calling in; guarded again
        // here because UNTimeIntervalNotificationTrigger requires a strictly positive interval.
        return;
    }

    UNMutableNotificationContent *content = [[UNMutableNotificationContent alloc] init];
    content.title = [NSString stringWithUTF8String:title];
    content.body = [NSString stringWithUTF8String:body];
    content.sound = [UNNotificationSound defaultSound];
    if (url != NULL && url[0] != '\0') {
        // Stored on the request itself so a click can resolve it even after Yobi has restarted.
        content.userInfo = @{ @"url": [NSString stringWithUTF8String:url] };
    }

    UNTimeIntervalNotificationTrigger *trigger = [UNTimeIntervalNotificationTrigger triggerWithTimeInterval:interval repeats:NO];
    NSString *nsIdentifier = [NSString stringWithUTF8String:identifier];
    UNNotificationRequest *request = [UNNotificationRequest requestWithIdentifier:nsIdentifier content:content trigger:trigger];

    [[UNUserNotificationCenter currentNotificationCenter] addNotificationRequest:request withCompletionHandler:nil];
}

void Yobi_CancelNotification(const char *identifier)
{
    NSString *nsIdentifier = [NSString stringWithUTF8String:identifier];
    UNUserNotificationCenter *center = [UNUserNotificationCenter currentNotificationCenter];
    [center removePendingNotificationRequestsWithIdentifiers:@[ nsIdentifier ]];
    [center removeDeliveredNotificationsWithIdentifiers:@[ nsIdentifier ]];
}
