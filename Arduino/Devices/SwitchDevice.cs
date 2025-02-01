using System;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chetch.Arduino.Devices;

public class SwitchDevice : ArduinoDevice
{

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
    public SwitchDevice(string name) : base(name)
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
