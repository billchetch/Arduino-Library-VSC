using System;

namespace Chetch.Arduino.Devices.Comms;

public class SerialPin : ArduinoDevice
{
    public SerialPin(byte id, string sid, string? name = null) : base(id, sid, name)
    {
    }
}
