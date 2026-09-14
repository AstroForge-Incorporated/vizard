// Run from the repo root:
// xcrun clang -fobjc-arc -framework AppKit -framework UniformTypeIdentifiers tests/test_macos_file_access.m -o /tmp/vizard-file-access-test
// /tmp/vizard-file-access-test
#import "../native/macos/VizardFileAccess.m"
#import <assert.h>

@interface TestDrag : NSObject
@property(nonatomic, strong) NSPasteboard *draggingPasteboard;
@property(nonatomic) NSDragOperation draggingSourceOperationMask;
@end
@implementation TestDrag
@end

@interface OriginalDelegate : NSObject <NSWindowDelegate>
@property(nonatomic) BOOL called;
@end
@implementation OriginalDelegate
- (BOOL)windowShouldClose:(NSWindow *)sender
{
    self.called = YES;
    return NO;
}
@end

int main(void)
{
    @autoreleasepool {
        [NSApplication sharedApplication];
        [NSApp setActivationPolicy:NSApplicationActivationPolicyRegular];
        NSWindow *window = [[NSWindow alloc] initWithContentRect:NSMakeRect(0, 0, 320, 240)
            styleMask:NSWindowStyleMaskTitled backing:NSBackingStoreBuffered defer:NO];
        OriginalDelegate *original = [OriginalDelegate new];
        window.delegate = original;
        [window makeKeyAndOrderFront:nil];
        [window makeMainWindow];
        VizardEnableFileDrop();
        assert(window.delegate != original);
        assert(![window.delegate windowShouldClose:window] && original.called);

        NSString *directory = [NSTemporaryDirectory() stringByAppendingPathComponent:NSUUID.UUID.UUIDString];
        [[NSFileManager defaultManager] createDirectoryAtPath:directory withIntermediateDirectories:YES attributes:nil error:nil];
        NSURL *recording = [NSURL fileURLWithPath:[directory stringByAppendingPathComponent:@"long path — asteroid α.BIN"]];
        [@"placeholder" writeToURL:recording atomically:YES encoding:NSUTF8StringEncoding error:nil];
        TestDrag *drag = [TestDrag new];
        drag.draggingPasteboard = [NSPasteboard pasteboardWithUniqueName];
        drag.draggingSourceOperationMask = NSDragOperationCopy;
        [drag.draggingPasteboard writeObjects:@[recording]];
        assert([(id<NSDraggingDestination>)window draggingEntered:(id)drag] == NSDragOperationCopy);
        assert([(id<NSDraggingDestination>)window prepareForDragOperation:(id)drag]);
        assert([(id<NSDraggingDestination>)window performDragOperation:(id)drag]);
        assert([[NSString stringWithUTF8String:VizardTakeFileDrop()] isEqualToString:recording.path]);
        assert(VizardTakeFileDrop() == NULL);

        [drag.draggingPasteboard clearContents];
        [drag.draggingPasteboard writeObjects:@[recording, recording]];
        assert([(id<NSDraggingDestination>)window draggingEntered:(id)drag] == NSDragOperationNone);
        assert(![(id<NSDraggingDestination>)window performDragOperation:(id)drag]);
        [drag.draggingPasteboard clearContents];
        [drag.draggingPasteboard writeObjects:@[[NSURL fileURLWithPath:directory]]];
        assert([(id<NSDraggingDestination>)window draggingEntered:(id)drag] == NSDragOperationNone);
        [drag.draggingPasteboard clearContents];
        [drag.draggingPasteboard writeObjects:@[[NSURL fileURLWithPath:@"/does/not/exist.bin"]]];
        assert([(id<NSDraggingDestination>)window draggingEntered:(id)drag] == NSDragOperationNone);
        NSURL *textFile = [recording.URLByDeletingPathExtension URLByAppendingPathExtension:@"txt"];
        [@"text" writeToURL:textFile atomically:YES encoding:NSUTF8StringEncoding error:nil];
        [drag.draggingPasteboard clearContents];
        [drag.draggingPasteboard writeObjects:@[textFile]];
        assert([(id<NSDraggingDestination>)window draggingEntered:(id)drag] == NSDragOperationNone);
        [drag.draggingPasteboard releaseGlobally];
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, NSEC_PER_SEC / 4), dispatch_get_main_queue(), ^{
            [(NSOpenPanel *)NSApp.modalWindow cancel:nil];
        });
        assert(VizardOpenFile(directory.UTF8String, "bin") == NULL);
        VizardDisableFileDrop();
        assert(window.delegate == original);
        [[NSFileManager defaultManager] removeItemAtPath:directory error:nil];
        puts("PASS native file drops: window dispatch, delegate forwarding, Unicode paths, single-file/type checks, picker cancellation, teardown");
    }
}
