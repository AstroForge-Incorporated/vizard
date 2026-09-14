#import <AppKit/AppKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>

// Returned UTF-8 strings stay alive until the next call on the Unity main thread.
static NSString *selectedPath;
static NSString *pendingDrop;
static NSString *deliveredDrop;

static NSString *RecordingOnPasteboard(NSPasteboard *pasteboard)
{
    NSArray<NSURL *> *files = [pasteboard readObjectsForClasses:@[[NSURL class]]
        options:@{NSPasteboardURLReadingFileURLsOnlyKey: @YES}];
    if (files.count != 1) return nil;
    NSURL *file = files.firstObject;
    if ([file.pathExtension caseInsensitiveCompare:@"bin"] != NSOrderedSame) return nil;
    NSNumber *regularFile;
    if (![file getResourceValue:&regularFile forKey:NSURLIsRegularFileKey error:nil] || !regularFile.boolValue)
        return nil;
    return file.path;
}

// NSWindow forwards drag events to its delegate. Preserve Unity's other window callbacks.
@interface VizardDropDelegate : NSObject <NSWindowDelegate>
@property(nonatomic, strong) id<NSWindowDelegate> unityDelegate;
@end

@implementation VizardDropDelegate
- (BOOL)respondsToSelector:(SEL)selector
{
    return [super respondsToSelector:selector] || [self.unityDelegate respondsToSelector:selector];
}
- (id)forwardingTargetForSelector:(SEL)selector
{
    return self.unityDelegate;
}
- (NSDragOperation)draggingEntered:(id<NSDraggingInfo>)sender
{
    return RecordingOnPasteboard(sender.draggingPasteboard)
        ? sender.draggingSourceOperationMask & NSDragOperationCopy : NSDragOperationNone;
}
- (NSDragOperation)draggingUpdated:(id<NSDraggingInfo>)sender
{
    return [self draggingEntered:sender];
}
- (BOOL)prepareForDragOperation:(id<NSDraggingInfo>)sender
{
    return [self draggingEntered:sender] != NSDragOperationNone;
}
- (BOOL)performDragOperation:(id<NSDraggingInfo>)sender
{
    NSString *path = RecordingOnPasteboard(sender.draggingPasteboard);
    if (!path) return NO;
    pendingDrop = path;
    return YES;
}
@end

static VizardDropDelegate *dropDelegate;
static __weak NSWindow *dropWindow;

void VizardDisableFileDrop(void)
{
    if (dropWindow.delegate == dropDelegate) dropWindow.delegate = dropDelegate.unityDelegate;
    [dropWindow unregisterDraggedTypes];
    dropWindow = nil;
    dropDelegate = nil;
}

void VizardEnableFileDrop(void)
{
    NSWindow *window = NSApp.mainWindow;
    if (!window)
        for (NSWindow *candidate in NSApp.windows)
            if (candidate.canBecomeMainWindow && ![candidate isKindOfClass:[NSPanel class]])
            {
                window = candidate;
                break;
            }
    if (!window || [window isKindOfClass:[NSPanel class]] || window == dropWindow) return;
    VizardDisableFileDrop();
    dropDelegate = [VizardDropDelegate new];
    dropDelegate.unityDelegate = window.delegate;
    dropWindow = window;
    window.delegate = dropDelegate;
    [window registerForDraggedTypes:@[NSPasteboardTypeFileURL]];
}

const char *VizardTakeFileDrop(void)
{
    deliveredDrop = pendingDrop;
    pendingDrop = nil;
    return deliveredDrop.UTF8String;
}

const char *VizardOpenFile(const char *initialDirectory, const char *extensions)
{
    NSOpenPanel *panel = [NSOpenPanel openPanel];
    panel.canChooseDirectories = NO;
    panel.allowsMultipleSelection = NO;
    panel.title = @"Open Vizard file";
    panel.directoryURL = [NSURL fileURLWithPath:[NSString stringWithUTF8String:initialDirectory] isDirectory:YES];
    NSMutableArray<UTType *> *types = [NSMutableArray array];
    for (NSString *extension in [[NSString stringWithUTF8String:extensions] componentsSeparatedByString:@"|"])
    {
        UTType *type = [UTType typeWithFilenameExtension:extension];
        if (type) [types addObject:type];
    }
    panel.allowedContentTypes = types;
    selectedPath = [panel runModal] == NSModalResponseOK ? panel.URL.path : nil;
    return selectedPath.UTF8String;
}
