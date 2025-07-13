using System;


namespace Chetch.Arduino.Devices.Infrared;

public class IRReceiver : ArduinoDevice
{
    public IRReceiver(byte id, String sid, String? name = null) : base(id, sid, name)
    {
        //empty for now
    }

    public IRReceiver(String sid, String? name = null) : this(0, sid, name) { }
}
