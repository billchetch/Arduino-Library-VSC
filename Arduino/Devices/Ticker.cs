using System;

namespace Chetch.Arduino.Devices;

public class Ticker : ArduinoDevice
{
    [ArduinoMessageMap(Messaging.MessageType.DATA, 0)]
    [ArduinoMessageMap(Messaging.MessageType.NOTIFICATION, 0)]
    public int Count { get; internal set; } = -1;


    [ArduinoMessageMap(Messaging.MessageType.DATA, 0)]
    public int Count2 { get; internal set; } = -1;


    public Ticker(string name) : base(name)
    {
    }

    public override void HandleMessage(ArduinoMessage message)
    {
        base.HandleMessage(message);
        Console.WriteLine("Value of Count/Count2 is: {0}/{1}", Count, Count2);
    }
}
