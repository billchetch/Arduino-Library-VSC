using Chetch.Arduino;
using Chetch.Arduino.Devices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.Payloads;

namespace Arduino.Tests;

[TestClass]
public sealed class TestBoard
{
    [TestMethod]
    public void Connect()
    {
        var board = new ArduinoBoard("test");

        try
        {
            board.Connection = Settings.GetConnection();
            board.Connection.Connected += (sender, connected) =>
            {
                Console.WriteLine("Board {0} connected: {1}", board.SID, connected);
            };
            board.Connection.DataReceived += (sender, bytes) =>
            {
                Console.WriteLine("Connection for board {0} received: {1} bytes", board.SID, bytes.Length);
            };
            
            board.Ready += (sender, ready) =>
            {
                Console.WriteLine("Board {0} ready: {1}", board.SID, ready);
            };

            board.MessageSent += (sender, message) =>
            {
                Console.WriteLine("Message sent of type {0} targeting {1} ", message.Type, message.Target);
            };

            board.MessageReceived += (sender, message) =>
            {
                Console.WriteLine("Message received of type {0} from {1} targeting {2}", message.Type, message.Sender, message.Target);
                switch (message.Type)
                {
                    case Chetch.Messaging.MessageType.STATUS_RESPONSE:
                        Console.WriteLine("Devices: {0}", board.DeviceCount);
                        Console.WriteLine("Memory Available: {0}", board.FreeMemory);
                        break;
                }
            };

            board.ExceptionThrown += (sender, eargs) =>
            {
                Console.WriteLine("Board {0} throws exception: {1}", board.SID, eargs.GetException().Message);
            };

            Console.WriteLine("Beginning board {0}...", board.SID);
            board.Begin();
            Console.WriteLine("Board {0} has begun!", board.SID);

            var started = DateTime.Now;
            while (!board.IsReady && (DateTime.Now - started).TotalSeconds < 10)
            {
                Thread.Sleep(1000);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            Console.WriteLine("Ending test for board {0}", board.SID);
            board.End();
            Thread.Sleep(500);
            Console.WriteLine("Board {0} has ended", board.SID);
        }
    }

    [TestMethod]
    public void RepeatConnect()
    {
        for (int i = 0; i < 10; i++)
        {
            Console.WriteLine("Connect number {0}", i + 1);
            Connect();
            Thread.Sleep(1000);
            Console.WriteLine("-----------");
        }
    }

    [TestMethod]
    public void ConnectToBoardWithTicker()
    {
        var board = new ArduinoBoard("test");
        var ticker = new Ticker(ArduinoBoard.START_DEVICE_IDS_AT, "ticker");

        try
        {
            ticker.ReportInterval = 500;
            ticker.Ticked += (sender, count) =>
            {
                Console.WriteLine("Ticker count = {0}", count);
            };
            board.AddDevice(ticker);
            board.Connection = Settings.GetConnection();
            board.Ready += (sender, ready) =>
            {
                Console.WriteLine("Board {0} ready: {1}", board.SID, ready);
            };
            board.ExceptionThrown += (sender, eargs) =>
            {
                Console.WriteLine("Board {0} throws exception: {1}", board.SID, eargs.GetException().Message);
            };

            Console.WriteLine("Beginning board {0}...", board.SID);
            board.Begin();
            Console.WriteLine("Board {0} has begun!", board.SID);

            while (ticker.Count < 10)
            {
                Thread.Sleep(1000);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            board.End();
            Thread.Sleep(500);
            Console.WriteLine("Board {0} has ended", board.SID);
        }
    }
}
