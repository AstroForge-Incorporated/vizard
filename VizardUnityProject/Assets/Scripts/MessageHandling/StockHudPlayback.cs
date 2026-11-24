using System.Collections.Generic;
using System.IO;
using VizProtobufferMessage;

// Each HUD dialog replaces the previous sample. Frames between updates retain that sample,
// including after a seek that puts the update outside the current message buffer.
internal static class StockHudPlayback
{
    private static readonly List<(int frame, long offset)> fileUpdates = new();
    private static readonly Comparer<(int frame, long offset)> frameOrder =
        Comparer<(int frame, long offset)>.Create((left, right) => left.frame.CompareTo(right.frame));
    private static string filePath;
    private static VizMessage firstFrame, lastFrame;
    private static string spacecraft;
    private static int lastIndex = -1, lastFileUpdate = -1;
    private static StockHud sample;

    internal static void StartFile(string path)
    {
        filePath = path;
        fileUpdates.Clear();
        firstFrame = lastFrame = null;
        lastIndex = lastFileUpdate = -1;
        sample = null;
    }

    internal static void IndexFileFrame(int index, long offset, VizMessage message)
    {
        if (LastDialog(message) != null) fileUpdates.Add((index, offset));
    }

    private static VizEventDialog LastDialog(VizMessage message)
    {
        for (int i = message.VizEventDialogs.Count - 1; i >= 0; i--)
            if (message.VizEventDialogs[i].EventHandlerID == "HUD") return message.VizEventDialogs[i];
        return null;
    }

    private static StockHud ReadSample(VizEventDialog dialog)
    {
        return dialog != null && StockHud.IsTelemetry(dialog) ? new StockHud(dialog.DisplayString, spacecraft) : null;
    }

    private static StockHud FileSample(int index)
    {
        int entry = fileUpdates.BinarySearch((index, 0), frameOrder);
        if (entry < 0) entry = ~entry - 1;
        if (entry == lastFileUpdate) return sample;
        if (entry < 0)
        {
            lastFileUpdate = -1;
            return null;
        }

        var update = fileUpdates[entry];
        var message = MessageList.GetMessageAtIndex(update.frame);
        if (message == null)
        {
            try
            {
                using (var input = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    input.Position = update.offset;
                    message = VizMessage.Parser.ParseDelimitedFrom(input);
                }
            }
            catch (IOException)
            {
                lastFileUpdate = -1;
                return null;
            }
        }
        lastFileUpdate = entry;
        return ReadSample(LastDialog(message));
    }

    private static StockHud LiveSample(int index)
    {
        for (int i = index; i >= 0; i--)
        {
            var message = MessageList.GetMessageAtIndex(i);
            if (message == null) return null;
            if (i == lastIndex && ReferenceEquals(lastFrame, message)) return sample;
            var dialog = LastDialog(message);
            if (dialog != null) return ReadSample(dialog);
        }
        return null;
    }

    internal static StockHud Current
    {
        get
        {
            if (MessageList.TimestepsTotal == 0) return null;
            if (!ReferenceEquals(firstFrame, MessageList.FirstMessage))
            {
                firstFrame = MessageList.FirstMessage;
                spacecraft = StockHud.SingleSpacecraft(firstFrame);
                lastFrame = null;
                lastIndex = lastFileUpdate = -1;
                sample = null;
            }
            if (spacecraft == null || MessageList.DesiredNextIndex >= 0) return sample;
            int index = MessageList.CurrentIndex;
            var frame = MessageList.GetMessageAtIndex(index);
            if (frame == null) return null;
            if (lastIndex == index && ReferenceEquals(lastFrame, frame)) return sample;
            sample = DataManager.IsLiveSim ? LiveSample(index) : FileSample(index);
            lastIndex = index;
            lastFrame = frame;
            return sample;
        }
    }
}
