// Makes Unity's own NSWindow transparent, borderless, and (optionally) always-on-top - the
// foundation for a Desktop Mate-style floating character window (roadmap Phase 3, "Desktop
// Companion 功能": 透明視窗、永遠置頂、拖拉角色).
//
// Rebuild with: Assets/Plugins/macOS/Native/build_window_control.sh

#import <Cocoa/Cocoa.h>
#import <QuartzCore/QuartzCore.h>

// Cached once resolved: NSApp.windows can include Unity-internal auxiliary windows (e.g. for
// text input) beyond the one visible player window. Re-scanning "the first visible window" on
// every call broke as soon as something else hid the real main window - the scan would then
// latch onto one of those auxiliary windows instead. Resolving once, while the real window is
// still the only visible one (during the initial Yobi_MakeWindowTransparent call), and reusing
// that reference afterward avoids ever being confused by them.
static NSWindow *gMainWindow = nil;

static NSWindow *Yobi_FindMainWindow(void)
{
    if (gMainWindow != nil) {
        return gMainWindow;
    }

    // Unity doesn't expose its NSWindow via any public API. A plain Standalone Player only
    // ever creates the one window, so the first visible one NSApplication knows about is it.
    for (NSWindow *window in [NSApp windows]) {
        if (window.isVisible) {
            gMainWindow = window;
            return window;
        }
    }
    gMainWindow = [NSApp windows].firstObject;
    return gMainWindow;
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

void Yobi_SetWindowVisible(bool visible)
{
    NSWindow *window = Yobi_FindMainWindow();
    if (window == nil) {
        return;
    }
    if (visible) {
        [window makeKeyAndOrderFront:nil];
    } else {
        [window orderOut:nil];
    }
}

bool Yobi_IsWindowVisible(void)
{
    NSWindow *window = Yobi_FindMainWindow();
    return window != nil && window.isVisible;
}

void Yobi_GetWindowPosition(double *outX, double *outY)
{
    NSWindow *window = Yobi_FindMainWindow();
    NSRect frame = window != nil ? window.frame : NSZeroRect;
    if (outX != NULL) {
        *outX = frame.origin.x;
    }
    if (outY != NULL) {
        *outY = frame.origin.y;
    }
}

// Picks a single real screen to clamp against, rather than the bounding union of every screen:
// vertically or diagonally offset displays leave gaps inside that union's rectangle, and a
// saved position landing in such a gap would pass a union-based clamp while still placing the
// whole window outside of every actual display. Prefers whichever screen the proposed window
// rect overlaps most; if none overlap at all (e.g. the gap itself, or a disconnected monitor),
// falls back to whichever screen's center is nearest.
static NSScreen *Yobi_FindScreenForPosition(double x, double y, CGFloat width, CGFloat height)
{
    NSArray<NSScreen *> *screens = [NSScreen screens];
    if (screens.count == 0) {
        return nil;
    }

    NSRect proposedFrame = NSMakeRect(x, y, width, height);

    NSScreen *bestOverlap = nil;
    CGFloat bestOverlapArea = 0;
    for (NSScreen *screen in screens) {
        NSRect intersection = NSIntersectionRect(proposedFrame, screen.visibleFrame);
        CGFloat area = intersection.size.width * intersection.size.height;
        if (area > bestOverlapArea) {
            bestOverlapArea = area;
            bestOverlap = screen;
        }
    }
    if (bestOverlap != nil) {
        return bestOverlap;
    }

    NSScreen *nearest = screens.firstObject;
    CGFloat nearestDistanceSquared = CGFLOAT_MAX;
    NSPoint proposedCenter = NSMakePoint(x + width / 2.0, y + height / 2.0);
    for (NSScreen *screen in screens) {
        NSPoint screenCenter = NSMakePoint(NSMidX(screen.visibleFrame), NSMidY(screen.visibleFrame));
        CGFloat dx = screenCenter.x - proposedCenter.x;
        CGFloat dy = screenCenter.y - proposedCenter.y;
        CGFloat distanceSquared = dx * dx + dy * dy;
        if (distanceSquared < nearestDistanceSquared) {
            nearestDistanceSquared = distanceSquared;
            nearest = screen;
        }
    }
    return nearest;
}

void Yobi_SetWindowPositionClamped(double x, double y)
{
    NSWindow *window = Yobi_FindMainWindow();
    if (window == nil) {
        return;
    }

    NSRect frame = window.frame;
    NSScreen *targetScreen = Yobi_FindScreenForPosition(x, y, frame.size.width, frame.size.height);

    if (targetScreen != nil) {
        NSRect visibleFrame = targetScreen.visibleFrame;

        CGFloat minX = visibleFrame.origin.x;
        CGFloat minY = visibleFrame.origin.y;
        CGFloat maxX = visibleFrame.origin.x + visibleFrame.size.width - frame.size.width;
        CGFloat maxY = visibleFrame.origin.y + visibleFrame.size.height - frame.size.height;

        // maxX/maxY can end up below minX/minY when the window is wider/taller than the
        // target screen - clamp toward the lower bound rather than produce an inverted (and
        // effectively arbitrary) range.
        if (maxX < minX) {
            maxX = minX;
        }
        if (maxY < minY) {
            maxY = minY;
        }

        x = MAX(minX, MIN(x, maxX));
        y = MAX(minY, MIN(y, maxY));
    }

    [window setFrameOrigin:NSMakePoint(x, y)];
}
