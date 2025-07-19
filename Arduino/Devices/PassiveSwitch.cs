using System;

namespace Chetch.Arduino.Devices;

public class PassiveSwitch : SwitchDevice
{
    public PassiveSwitch(String sid, String? name) : base(sid, SwitchMode.PASSIVE, name) { }
}
