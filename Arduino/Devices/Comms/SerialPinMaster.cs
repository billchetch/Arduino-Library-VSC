using System;

namespace Chetch.Arduino.Devices.Comms;

public class SerialPinMaster : SerialPin
{
    public SerialPinMaster(byte id, string sid, string? name = null) : base(id, sid, name)
    {
    }

    public void Send(byte b2s)
    {
        SendCommand(DeviceCommand.SEND, b2s);
    }
}
