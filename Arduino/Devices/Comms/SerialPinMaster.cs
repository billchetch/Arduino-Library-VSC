using System;

namespace Chetch.Arduino.Devices.Comms;

public class SerialPinMaster : SerialPin
{
    public SerialPinMaster(string sid, string? name = null) : base(sid, name)
    {
    }

    public void Send(byte b2s)
    {
        SendCommand(DeviceCommand.SEND, b2s);
    }
}
