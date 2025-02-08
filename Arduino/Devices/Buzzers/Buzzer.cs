using System;

namespace Chetch.Arduino.Devices.Buzzers;

public class Buzzer : SwitchDevice
{
    public Buzzer(byte id, string name) : base(id, name)
    {
    }
}
