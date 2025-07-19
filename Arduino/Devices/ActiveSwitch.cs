using System;

namespace Chetch.Arduino.Devices;

public class ActiveSwitch : SwitchDevice
{
    public ActiveSwitch(String sid, String? name) : base(sid, SwitchMode.ACITVE, name) { }
}
