/*
 ISC License

 Copyright (c) 2025, Autonomous Vehicle Systems Lab, University of Colorado at Boulder

 Permission to use, copy, modify, and/or distribute this software for any
 purpose with or without fee is hereby granted, provided that the above
 copyright notice and this permission notice appear in all copies.

 THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
 WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
 MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
 ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
 WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
 ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
 OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

 */
using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System.IO;
using VizProtobufferMessage;


public class TestMessageList : MonoBehaviour
{

    public string RunMessageListTestSuite()
    {
        string testFilePath = Application.dataPath + "/Tests/TestMessageFiles/bufferTestFileWithMissingFramesBufferLimit10k.bin";
        // The bufferTestFileWithMissingFrames.bin test file contains is missing every 100th frame and 
        // also contains a repeat of the first hundred or so frames before continuing on. This allows some testing
        // for continuity issues in the file reading/buffering.
        
        Assert.AreEqual(10000000, MessageList.GetBufferLimit()); //Check the default buffer limit has not changed
        Assert.AreEqual(10, MessageList.GetIndexSpacing()); //Check the default message count between file indices has not changed

        TestStockHudAcrossBufferSeeks();
        bool smallBufferPass = Test_LoadSmallBuffer(testFilePath);
        bool fullFilePass = Test_LoadEntireFileIntoBuffer(testFilePath);

        return "\t All MessageList.cs tests passed.\n";
    }

    private static void TestStockHudAcrossBufferSeeks()
    {
        string path = Path.GetTempFileName();
        try
        {
            using (var output = File.Create(path))
            {
                for (int frame = 0; frame < 100; frame++)
                {
                    var message = new VizMessage {
                        CurrentTime = new VizMessage.Types.TimeStamp { FrameNumber = frame + 1, SimTimeElapsed = frame },
                        Spacecraft = { new VizMessage.Types.Spacecraft { SpacecraftName = "Test spacecraft" } }
                    };
                    if (frame == 0) message.Settings = new VizMessage.Types.VizSettingsPb();
                    if (frame == 3 || frame == 50)
                    {
                        message.VizEventDialogs.Add(new VizEventDialog {
                            EventHandlerID = "HUD",
                            DisplayString = $"Live Data:\nSC omega_b truth: [{frame}, 0, 0] [deg/s]"
                        });
                    }
                    Google.Protobuf.MessageExtensions.WriteDelimitedTo(message, output);
                }
            }

            bool loaded = MessageList.FirstMessageBuffersReadFromFile(path, 256);
            Assert.IsTrue(loaded);
            Assert.IsNull(StockHud.Current);
            foreach (var (destination, sampleFrame) in new[] { (80, 50), (35, 3) })
            {
                MessageList.SetNextIndex(destination, true);
                if (MessageList.DesiredNextIndex >= 0) MessageList.SetNextIndex(destination);
                Assert.IsNull(MessageList.GetMessageAtIndex(sampleFrame));
                Assert.AreEqual((float)sampleFrame, StockHud.Current.Numbers("SC omega_b truth")[0]);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
    
    public bool Test_LoadSmallBuffer(string testFilePath){
        
        int expectedMessageCountInFirstBuffer = 11;
        int expectedTotalMessages = 595;
        
        MessageList.FirstMessageBuffersReadFromFile(testFilePath);
        //Check that the test file's buffer setting was applied
        Assert.AreEqual(10000, MessageList.GetBufferLimit()); 
        //Check the default message count between file indices has not changed
        Assert.AreEqual(10, MessageList.GetIndexSpacing()); 
        
        SubTest_FirstLoadOfBuffer(expectedMessageCountInFirstBuffer, expectedTotalMessages);
        
        SubTest_MarchThroughAllIndices();
        
        //First message is larger because of Settings submessage, subsequent buffers fit 12 messages at 10k buffer limit
        SubTest_JumpIndices(expectedMessageCountInFirstBuffer+1); 

        return true;
    }

    public bool Test_LoadEntireFileIntoBuffer(string testFilePath)
    {
        // TEST ENTIRE FILE LOADED (NO BUFFERING)
        int expectedMessageCountInFirstBuffer = 595;
        int expectedTotalMessages = 595;
        
        //Change the buffer limit to large enough to load entire file
        MessageList.FirstMessageBuffersReadFromFile(testFilePath, 1000000);
        
        //Check that the test file's buffer setting was applied
        Assert.AreEqual(509663, MessageList.GetBufferLimit()); 
        
        //Check the default message count between file indices has not changed
        Assert.AreEqual(10, MessageList.GetIndexSpacing()); 

        SubTest_FirstLoadOfBuffer(expectedMessageCountInFirstBuffer, expectedTotalMessages);

        SubTest_MarchThroughAllIndices();
        
        SubTest_JumpIndices(expectedMessageCountInFirstBuffer);
        
        return true;
    }

    private void SubTest_FirstLoadOfBuffer(int expectedMessageCountInFirstBuffer, int expectedTotalMessages)
    {
        Assert.AreEqual(expectedMessageCountInFirstBuffer, MessageList.NumberOfMessagesInBuffer()); 
        Assert.AreEqual(expectedTotalMessages, MessageList.TimestepsTotal); 

        double[,] scPositions = MessageList.GetPositionHistoryBSK(true, 0);
        double[,] planetPositions = MessageList.GetPositionHistoryBSK(false, 0);
        Assert.AreEqual(expectedMessageCountInFirstBuffer, scPositions.GetLength(0));
        Assert.AreEqual(expectedMessageCountInFirstBuffer, planetPositions.GetLength(0));
        
        double[,] scVelocities = MessageList.GetVelocityHistoryBSK(true, 0);
        double[,] planetVelocities = MessageList.GetVelocityHistoryBSK(false, 0);
        Assert.AreEqual(expectedMessageCountInFirstBuffer, scVelocities.GetLength(0));
        Assert.AreEqual(expectedMessageCountInFirstBuffer, planetVelocities.GetLength(0));
    }
    
    private void SubTest_MarchThroughAllIndices()
    {
        Assert.AreEqual(1, MessageList.CurrentMessage.CurrentTime.FrameNumber);
        int tryCount = 0;
        for (int i = 0; i < MessageList.TimestepsTotal; i++)
        {
            MessageList.SetNextIndex(i);
            //Check that currentIndex advanced,
            // but if in buffer load, nextIndex will need to
            // be requested again
            if ((MessageList.CurrentIndex != i)&&(tryCount<2))
            {
                i -= 1;
                tryCount++;
            }
            else
            {
                tryCount = 0;
            } 
        }
        Assert.AreEqual(594, MessageList.CurrentIndex);
        Assert.AreEqual(601, MessageList.CurrentMessage.CurrentTime.FrameNumber);
        
        MessageList.SetNextIndex(595);
        Assert.AreEqual(0, MessageList.CurrentIndex); 
    }

    private void SubTest_JumpIndices(int expectedMessageCountInBuffer)
    {
        MessageList.SetNextIndex(0);
        Assert.AreEqual(0, MessageList.CurrentIndex);
        
        //Try moving to middle of file 
        MessageList.SetNextIndex(100);
        MessageList.SetNextIndex(100); //Needed only if buffer load required by previous line
        Assert.AreEqual(100, MessageList.CurrentIndex);
        Assert.AreEqual(102, MessageList.CurrentMessage.CurrentTime.FrameNumber);
        Assert.AreEqual(expectedMessageCountInBuffer, MessageList.NumberOfMessagesInBuffer());
        
        //Try another jump
        MessageList.SetNextIndex(350);
        MessageList.SetNextIndex(350); //Needed only if buffer load required by previous line
        Assert.AreEqual(350, MessageList.CurrentIndex);
        Assert.AreEqual(354, MessageList.CurrentMessage.CurrentTime.FrameNumber);
        Assert.AreEqual(expectedMessageCountInBuffer, MessageList.NumberOfMessagesInBuffer());
        
        //Try a backwards jump
        MessageList.SetNextIndex(10);
        MessageList.SetNextIndex(10); //Needed only if buffer load required by previous line
        Assert.AreEqual(10, MessageList.CurrentIndex);
        Assert.AreEqual(11, MessageList.CurrentMessage.CurrentTime.FrameNumber);
        Assert.AreEqual(expectedMessageCountInBuffer, MessageList.NumberOfMessagesInBuffer());
    }
}
