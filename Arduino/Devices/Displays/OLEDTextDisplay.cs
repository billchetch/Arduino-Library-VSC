using System;
using Microsoft.EntityFrameworkCore.Storage.Json;

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

    public OLEDTextDisplay(String sid, String? name = null) : this(0, sid, name) { }

    public void DiplsayPreset(DisplayPreset preset, UInt16 displayAndLockFor = 0)
    {
        SendCommand(DeviceCommand.DISPLAY, preset, displayAndLockFor);
    }

    public void Print(String text, UInt16 cx = 0, UInt16 cy = 0)
    {
        SendCommand(DeviceCommand.PRINT, text, cx, cy);
    }

    public void Clear(UInt16 displayAndLockFor = 0)
    {
        DiplsayPreset(DisplayPreset.CLEAR, displayAndLockFor);
    }

    public void UpdateDisplay(byte updateTag)
    {
        SendCommand(DeviceCommand.UPDATE, updateTag);
    }
}
