using System;

namespace Chetch.Arduino.Devices.Displays;

public class GenericDisplay : ArduinoDevice
{
    #region Constants
    public const String GENERIC_DISPLAY_SID = "display";
    #endregion

    #region Classes and enums

    public enum RefreshRate
    {
        NO_REFRESH = 0,
        REFRESH_1HZ = 1000,
        REFRESH_10HZ = 100,
        REFRESH_50Hz = 20
    };  
    
    public enum DisplayPreset : byte
    {
        CLEAR,
        BOARD_STATS,
        HELLO_WORLD
    }
    #endregion

    #region Properties
    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 1)] //Initial value
    public UInt16 Rows { get; internal set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 2)] //Initial value
    public UInt16 Cols { get; internal set; } = 0;
    
    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 3)]
    public RefreshRate RefreshHz { get; internal set; } = RefreshRate.NO_REFRESH;
    #endregion


    #region Constructors
    public GenericDisplay(byte id, String sid, String? name = null) : base(id, sid, name)
    {
        //empty for now
    }

    public GenericDisplay(String sid, String? name = null) : this(0, sid, name) { }
    
    public GenericDisplay(String? name = null) : this(GENERIC_DISPLAY_SID, name){}
    #endregion


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
