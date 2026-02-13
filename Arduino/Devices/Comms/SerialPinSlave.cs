using System;

namespace Chetch.Arduino.Devices.Comms;

public class SerialPinSlave : SerialPin
{
    public SerialPinSlave(string sid, string? name = null) : base(sid, name)
    {
    }
}
