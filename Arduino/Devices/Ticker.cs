using System;

namespace Chetch.Arduino.Devices;

public class Ticker : ArduinoDevice
{
    [ArduinoMessageMap(Messaging.MessageType.DATA, 0)]
    public int Count { get; internal set; } = -1;


    public Ticker(string name) : base(name)
    {
    }

    public void Test()
    {
        SendCommand(DeviceCommand.DEACTIVATE);
    }
}
