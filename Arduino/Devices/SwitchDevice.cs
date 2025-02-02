using System;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chetch.Arduino.Devices;

public class SwitchDevice : ArduinoDevice
{
    #region Constants
    public const byte ERROR_SWITCH_MODE = 101;
    
    #endregion

    #region Properties    
    [ArduinoMessageMap(Messaging.MessageType.DATA, 0)]
    public bool PinState 
    { 
        get
        {
            return pinState;
        } 

        internal set
        {
            if(value != pinState)
            {
                pinState = value;
                Switched?.Invoke(this, pinState);
            }
        }
    }
    #endregion

    #region Events
    public EventHandler<bool>? Switched;
    #endregion

    #region Fields
    bool pinState = false;
    #endregion

    #region Constructors
    public SwitchDevice(byte id, string name) : base(id, name)
    {

    }
    #endregion

    #region Methods
    public void TurnOn()
    {
        SendCommand(DeviceCommand.ON);
    }

    public void TurnOff()
    {
        SendCommand(DeviceCommand.OFF);
    }
    #endregion
}
