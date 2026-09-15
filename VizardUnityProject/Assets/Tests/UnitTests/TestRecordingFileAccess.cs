using System;
using System.IO;
using Google.Protobuf;
using UnityEngine.Assertions;
using VizProtobufferMessage;

public static class TestRecordingFileAccess
{
    public static string Run()
    {
        string path = Path.Combine(Path.GetTempPath(), $"Vizard recording α {Guid.NewGuid()}.bin");
        try
        {
            var frame = new VizMessage { CurrentTime = new VizMessage.Types.TimeStamp() };
            using (var stream = File.Create(path)) frame.WriteDelimitedTo(stream);
            Assert.IsFalse(MacFileAccess.CanOpenRecording(path, out _));
            frame.CurrentTime.SimTimeElapsed = 1;
            using (var stream = File.Open(path, FileMode.Append)) frame.WriteDelimitedTo(stream);
            Assert.IsTrue(MacFileAccess.CanOpenRecording(path, out _));

            File.WriteAllBytes(path, new byte[] { 0x80 }); // Truncated protobuf length prefix.
            Assert.IsFalse(MacFileAccess.CanOpenRecording(path, out _));
            using (var stream = File.Create(path)) new VizMessage().WriteDelimitedTo(stream);
            Assert.IsFalse(MacFileAccess.CanOpenRecording(path, out _));
            TestLoadingProgress(path);
            File.Delete(path);
            Assert.IsFalse(MacFileAccess.CanOpenRecording(path, out _));
            return "\t Recording preflight tests passed.\n";
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void TestLoadingProgress(string path)
    {
        const int frameCount = 128;
        var frame = new VizMessage
        {
            CurrentTime = new VizMessage.Types.TimeStamp(),
            Settings = new VizMessage.Types.VizSettingsPb { MessageBufferSize = 128 }
        };
        using (var stream = File.Create(path))
        {
            for (int i = 0; i < frameCount; i++)
            {
                frame.CurrentTime.SimTimeElapsed = i * 0.5;
                frame.WriteDelimitedTo(stream);
            }
        }

        float previous = -1;
        foreach (float progress in MessageList.ReadFileWithProgress(path))
        {
            if (previous < 0) Assert.AreEqual(0f, progress);
            Assert.IsTrue(progress >= previous && progress <= 1);
            Assert.IsTrue(MessageList.InBufferLoad);
            previous = progress;
        }
        Assert.AreEqual(1f, previous);
        Assert.IsFalse(MessageList.InBufferLoad);
        Assert.AreEqual(frameCount, MessageList.TimestepsTotal);
        Assert.AreEqual(0.5, MessageList.TimeStepSize);
        Assert.IsTrue(MessageList.LoadedMessagesCount < frameCount);
        MessageList.SetNextIndex(frameCount - 1);
        MessageList.SetNextIndex(frameCount - 1);
        Assert.AreEqual((frameCount - 1) * 0.5, MessageList.CurrentMessage.CurrentTime.SimTimeElapsed);
        MessageList.SetNextIndex(0);
        MessageList.SetNextIndex(0);
        Assert.AreEqual(0.0, MessageList.CurrentMessage.CurrentTime.SimTimeElapsed);

        // Disposing an interrupted load must release the loading state.
        using (var reader = MessageList.ReadFileWithProgress(path).GetEnumerator())
            Assert.IsTrue(reader.MoveNext());
        Assert.IsFalse(MessageList.InBufferLoad);

        // A failed load must also release it, so another file can be opened.
        File.WriteAllBytes(path, Array.Empty<byte>());
        bool rejected = false;
        try { foreach (float progress in MessageList.ReadFileWithProgress(path)) { } }
        catch (InvalidDataException) { rejected = true; }
        Assert.IsTrue(rejected);
        Assert.IsFalse(MessageList.InBufferLoad);
        using (var stream = File.Create(path))
        {
            frame.CurrentTime.SimTimeElapsed = 0;
            frame.WriteDelimitedTo(stream);
            frame.CurrentTime.SimTimeElapsed = 1;
            frame.WriteDelimitedTo(stream);
        }
        MessageList.FirstMessageBuffersReadFromFile(path);
        Assert.AreEqual(1.0, MessageList.TimeStepSize);
    }
}
