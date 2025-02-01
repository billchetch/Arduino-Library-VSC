using System;

namespace Chetch.Arduino.Devices;

public class SwitchDevice : ArduinoDevice
{

    #region Classes and enums
    public enum SwitchPosition
    {
        OFF = 0,
        ON = 1,
    }
    #endregion

    #region Properties
    #endregion

    #region Constructors
    public SwitchDevice(string name) : base(name)
    {

    }
    #endregion

    public void TurnOn()
    {
        SendCommand(DeviceCommand.ON);
    }
}
