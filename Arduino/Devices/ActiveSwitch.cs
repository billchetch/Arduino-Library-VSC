using System;

namespace Chetch.Arduino.Devices;

public class ActiveSwitch : SwitchDevice
{
    public ActiveSwitch(String sid, String? name = null) : base(sid, SwitchMode.ACITVE, name) { }
}
