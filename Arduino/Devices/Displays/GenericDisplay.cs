using System;
using System.Reflection;

namespace Chetch.Arduino.Devices.Displays;

public class GenericDisplay : ArduinoDevice
{
    #region Constants
    public const String GENERIC_DISPLAY_SID = "display";
    #endregion

    #region Classes and enums

    public enum RefreshRate : Int16
    {
        NOT_SET = -1,
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
    public RefreshRate RefreshHz { get; internal set; } = RefreshRate.NOT_SET;
    #endregion


    #region Constructors
    public GenericDisplay(byte id, String sid, String? name = null) : base(id, sid, name)
    {
        //empty for now
    }

    public GenericDisplay(String sid, String? name = null) : this(0, sid, name) { }
    
    public GenericDisplay(String? name = null) : this(GENERIC_DISPLAY_SID, name){}
    #endregion


    #region Messaging
    public override bool AssignMessageValue(PropertyInfo propertyInfo, object propertyValue, ArduinoMessage message)
    {
        /*if (propertyInfo.Name == "RefreshHz")
        {
            if (RefreshHz == RefreshRate.NOT_SET || (SwitchMode)propertyValue == Mode)
            {
                base.AssignMessageValue(propertyInfo, propertyValue, message);
                return true;
            }
            else
            {
                return false;
            }
        }*/
        return base.AssignMessageValue(propertyInfo, propertyValue, message);
    }

    public override ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        return base.HandleMessage(message);
    }
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
