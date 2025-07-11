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
                //Console.WriteLine("Connection for board {0} received: {1} bytes", board.SID, bytes.Length);
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
            board.End();
            Thread.Sleep(500);
            Console.WriteLine("Board {0} has ended", board.SID);
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
            ticker.Ticked += (sender, tick) =>
            {
                Console.WriteLine("Ticker tick = {0}", tick);
            };
            board.AddDevice(ticker);
            board.Connection = Settings.GetConnection();
            board.Ready += (sender, ready) =>
            {
                Console.WriteLine("Board {0} ready: {1}", board.SID, ready);
            };


            Console.WriteLine("Beginning board {0}...", board.SID);
            board.Begin();
            Console.WriteLine("Board {0} has begun!", board.SID);

            while (!board.IsReady && ticker.Count < 5)
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
