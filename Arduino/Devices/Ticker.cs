using System;

namespace Chetch.Arduino.Devices;

public class Ticker : ArduinoDevice
{
    [ArduinoMessageMap(Messaging.MessageType.DATA, 0)]
    public int Count { get; internal set; } = -1;


    public Ticker(byte id, string name) : base(id, name)
    {
    }

    public void Test()
    {
        SendCommand(DeviceCommand.DEACTIVATE);
    }
}
