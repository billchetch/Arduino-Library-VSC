using System;

namespace Chetch.Arduino.Devices.Comms;

public class MCP2515 : ArduinoDevice
{
    public MCP2515(byte id, string sid, string? name = null) : base(id, sid, name)
    {
        
    }
}
