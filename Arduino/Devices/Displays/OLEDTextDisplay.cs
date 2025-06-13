using System;

namespace Chetch.Arduino.Devices.Displays;

public class OLEDTextDisplay : ArduinoDevice
{

    public enum DisplayPreset
    {
        CLEAR,
        BOARD_STATS,
        HELLO_WORLD
    }

    public OLEDTextDisplay(byte id, String sid, String? name = null) : base(id, sid, name)
    {
        //empty for now
    }

    public void DiplsayPreset(DisplayPreset preset)
    {
        SendCommand(DeviceCommand.DISPLAY, preset);
    }
}
