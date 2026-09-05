// Makes Unity's own NSWindow transparent, borderless, and (optionally) always-on-top - the
// foundation for a Desktop Mate-style floating character window (roadmap Phase 3, "Desktop
// Companion 功能": 透明視窗、永遠置頂、拖拉角色).
//
// Rebuild with: Assets/Plugins/macOS/Native/build_window_control.sh

#import <Cocoa/Cocoa.h>
#import <QuartzCore/QuartzCore.h>

static NSWindow *Yobi_FindMainWindow(void)
{
    // Unity doesn't expose its NSWindow via any public API. A plain Standalone Player only
    // ever creates the one window, so the first visible one NSApplication knows about is it.
    for (NSWindow *window in [NSApp windows]) {
        if (window.isVisible) {
            return window;
        }
    }
    return [NSApp windows].firstObject;
}

void Yobi_MakeWindowTransparent(void)
{
    NSWindow *window = Yobi_FindMainWindow();
    if (window == nil) {
        NSLog(@"[YobiWindowControl] No window found to make transparent.");
        return;
    }

    window.opaque = NO;
    window.backgroundColor = [NSColor clearColor];
    window.hasShadow = NO;

    // Hide the title bar / traffic-light buttons to get a borderless *look* - a companion
    // character window shouldn't look like a normal document window. Dragging the character
    // itself (rather than a title bar) is a separate, not-yet-implemented feature.
    //
    // Deliberately NOT `window.styleMask = NSWindowStyleMaskBorderless`: replacing the style
    // mask of a window that still has full-screen primary collection behavior (Unity's
    // default player window does) makes -[NSWindow setStyleMask:] throw an uncaught
    // NSInternalInconsistencyException, which crashes the app (SIGABRT). Toggling visual
    // properties instead keeps the original style mask bits intact and avoids that crash.
    window.titlebarAppearsTransparent = YES;
    window.titleVisibility = NSWindowTitleHidden;
    window.styleMask |= NSWindowStyleMaskFullSizeContentView;
    [window standardWindowButton:NSWindowCloseButton].hidden = YES;
    [window standardWindowButton:NSWindowMiniaturizeButton].hidden = YES;
    [window standardWindowButton:NSWindowZoomButton].hidden = YES;

    // The NSWindow's own transparency isn't sufficient by itself: Unity renders through a
    // CAMetalLayer on the content view, and that layer defaults to opaque regardless of the
    // window's settings, which would still paint solid black over anything the Camera
    // doesn't cover.
    window.contentView.wantsLayer = YES;
    CALayer *contentLayer = window.contentView.layer;
    if ([contentLayer isKindOfClass:[CAMetalLayer class]]) {
        ((CAMetalLayer *)contentLayer).opaque = NO;
    }
    for (CALayer *sublayer in contentLayer.sublayers) {
        if ([sublayer isKindOfClass:[CAMetalLayer class]]) {
            ((CAMetalLayer *)sublayer).opaque = NO;
        }
    }
}

void Yobi_SetWindowAlwaysOnTop(bool alwaysOnTop)
{
    NSWindow *window = Yobi_FindMainWindow();
    if (window == nil) {
        return;
    }
    window.level = alwaysOnTop ? NSFloatingWindowLevel : NSNormalWindowLevel;
}
