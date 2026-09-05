// Shows a native "choose an image" open panel for the Room mode wallpaper picker.
//
// Rebuild with: Assets/Plugins/macOS/Native/build_file_picker.sh

#import <Cocoa/Cocoa.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>

typedef void (*YobiFilePickedCallback)(const char *path);

void Yobi_ShowImageOpenPanel(YobiFilePickedCallback callback)
{
    NSOpenPanel *panel = [NSOpenPanel openPanel];
    panel.allowsMultipleSelection = NO;
    panel.canChooseDirectories = NO;
    panel.canChooseFiles = YES;
    panel.allowedContentTypes = @[UTTypeImage];

    // beginWithCompletionHandler: (rather than runModal) so this doesn't block Unity's own run
    // loop while the panel is open - the completion handler fires on the main thread once the
    // user picks a file or cancels.
    [panel beginWithCompletionHandler:^(NSModalResponse result) {
        if (result == NSModalResponseOK && panel.URL != nil) {
            if (callback != NULL) {
                callback([panel.URL.path UTF8String]);
            }
        } else {
            if (callback != NULL) {
                callback(NULL);
            }
        }
    }];
}
