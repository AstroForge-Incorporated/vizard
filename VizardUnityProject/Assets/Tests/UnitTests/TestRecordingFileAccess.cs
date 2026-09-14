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
            File.Delete(path);
            Assert.IsFalse(MacFileAccess.CanOpenRecording(path, out _));
            return "\t Recording preflight tests passed.\n";
        }
        finally
        {
            File.Delete(path);
        }
    }
}
