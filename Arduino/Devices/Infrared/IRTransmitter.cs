using System;

namespace Chetch.Arduino.Devices.Infrared;

public class IRTransmitter : ArduinoDevice
{
    public IRTransmitter(byte id, String sid, String? name = null) : base(id, sid, name)
    {
        //empty for now
    }
}
