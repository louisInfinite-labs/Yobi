// Adds a menu bar (status bar) icon with a small menu for controlling the desktop companion
// window - "Show/Hide", switching between DesktopMate/Room mode, choosing the Room wallpaper,
// and "Quit" - so there is a way to reach the window before the actual clickable character
// (roadmap Phase 3) exists.
//
// Rebuild with: Assets/Plugins/macOS/Native/build_tray_icon.sh

#import <Cocoa/Cocoa.h>

typedef void (*YobiTrayActionCallback)(const char *action);

@interface YobiTrayMenuTarget : NSObject
- (void)onToggleVisibility:(id)sender;
- (void)onToggleMode:(id)sender;
- (void)onChooseWallpaper:(id)sender;
- (void)onQuit:(id)sender;
@end

static NSStatusItem *gStatusItem = nil;
static YobiTrayMenuTarget *gMenuTarget = nil;
static YobiTrayActionCallback gCallback = NULL;
static NSMenuItem *gToggleModeItem = nil;

@implementation YobiTrayMenuTarget

- (void)onToggleVisibility:(id)sender
{
    if (gCallback != NULL) {
        gCallback("toggle_visibility");
    }
}

- (void)onToggleMode:(id)sender
{
    if (gCallback != NULL) {
        gCallback("toggle_mode");
    }
}

- (void)onChooseWallpaper:(id)sender
{
    if (gCallback != NULL) {
        gCallback("choose_wallpaper");
    }
}

- (void)onQuit:(id)sender
{
    if (gCallback != NULL) {
        gCallback("quit");
    }
}

@end

void Yobi_SetTrayActionCallback(YobiTrayActionCallback callback)
{
    gCallback = callback;
}

void Yobi_CreateTrayIcon(void)
{
    if (gStatusItem != nil) {
        return;
    }

    gMenuTarget = [[YobiTrayMenuTarget alloc] init];

    gStatusItem = [[NSStatusBar systemStatusBar] statusItemWithLength:NSVariableStatusItemLength];
    gStatusItem.button.title = @"Yobi";

    NSMenu *menu = [[NSMenu alloc] init];

    NSMenuItem *toggleItem = [[NSMenuItem alloc] initWithTitle:@"Show/Hide Yobi"
                                                         action:@selector(onToggleVisibility:)
                                                  keyEquivalent:@""];
    toggleItem.target = gMenuTarget;
    [menu addItem:toggleItem];

    // Title is set for real right after creation via Yobi_SetToggleModeMenuTitle, once the
    // caller knows which mode was actually restored from disk.
    gToggleModeItem = [[NSMenuItem alloc] initWithTitle:@"Switch Mode"
                                                  action:@selector(onToggleMode:)
                                           keyEquivalent:@""];
    gToggleModeItem.target = gMenuTarget;
    [menu addItem:gToggleModeItem];

    NSMenuItem *wallpaperItem = [[NSMenuItem alloc] initWithTitle:@"Choose Room Wallpaper..."
                                                            action:@selector(onChooseWallpaper:)
                                                     keyEquivalent:@""];
    wallpaperItem.target = gMenuTarget;
    [menu addItem:wallpaperItem];

    [menu addItem:[NSMenuItem separatorItem]];

    NSMenuItem *quitItem = [[NSMenuItem alloc] initWithTitle:@"Quit Yobi"
                                                       action:@selector(onQuit:)
                                                keyEquivalent:@""];
    quitItem.target = gMenuTarget;
    [menu addItem:quitItem];

    gStatusItem.menu = menu;
}

void Yobi_SetToggleModeMenuTitle(const char *title)
{
    if (gToggleModeItem == nil || title == NULL) {
        return;
    }
    gToggleModeItem.title = [NSString stringWithUTF8String:title];
}

void Yobi_RemoveTrayIcon(void)
{
    if (gStatusItem != nil) {
        [[NSStatusBar systemStatusBar] removeStatusItem:gStatusItem];
        gStatusItem = nil;
    }
    gMenuTarget = nil;
    gToggleModeItem = nil;
}
