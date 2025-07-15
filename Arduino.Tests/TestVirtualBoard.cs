using System;
using Chetch.Utilities;

namespace Arduino.Tests;

[TestClass]
public class TestVirtualBoard
{
    [TestMethod]
    public void BoardWithNoDevices()
    {
        var cnnConfig = Settings.GetConnectionConfig("LocalSocket");
        var path = cnnConfig["Path"];
        var board = new ArduinoVirtualBoard(path);
        board.ExceptionThrown += (sender, eargs) =>
        {
            Console.WriteLine("Exception: {0}", eargs.GetException().Message);
        };
        board.MessageReceived += (sender, message) =>
        {
            Console.WriteLine("Message received: {0}", message.Type);
        };
        
        try
        {
            Console.WriteLine("Beginning virtual board");
            board.Begin();
            Console.WriteLine("Begun virtual board");
            Thread.Sleep(35000);

        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            Console.WriteLine("Ending virtual board");
            board.End();
        }
    }
}
