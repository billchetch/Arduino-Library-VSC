using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chetch.Arduino.Devices;

public class SwitchDevice : ArduinoDevice
{
    #region Constants
    public const byte ERROR_SWITCH_MODE = 101;
    
    #endregion

    #region Classes and Enums
    public enum SwitchMode
    {
        NOT_SET = 0,
        ACITVE = 1,
        PASSIVE =2,  
    }

    #endregion
    
    #region Properties    
    
    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 1)]
    public SwitchMode Mode { get; internal set; } = SwitchMode.NOT_SET;
    
    [ArduinoMessageMap(Messaging.MessageType.DATA, 0)]
    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 2)]
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
                if(IsReady && Switched != null)
                {
                    Switched.Invoke(this, pinState);
                }
            }
        }
    }
    
    //NOTE: if the switch nature was reversed (i.e. being 'on' was a low pin) then this would be different. for now this is just notatin.
    public bool IsOn => PinState; 
    public bool IsOff => !IsOn; 
    #endregion

    #region Events
    public event EventHandler<bool>? Switched;
    #endregion

    #region Fields
    bool pinState = false;
    #endregion

    #region Constructors
    public SwitchDevice(byte id, String sid, String? name = null) : base(id, sid, name)
    {}
    
    public SwitchDevice(String sid, String? name = null) : base(sid, name)
    {}
    #endregion

    #region Methods
    public override void OnReady(bool ready)
    {
        base.OnReady(ready);
        if (!ready)
        {
            PinState = false; //return to orignal pin state
        }
    }

    virtual public void TurnOn()
    {
        if(Mode != SwitchMode.ACITVE)
        {
            throw new Exception(String.Format("Switch {0} cannot be turned on or off because it is of mode {1}", UID, Mode));
        }
        SendCommand(DeviceCommand.ON);
    }

    virtual public void TurnOff()
    {
        if(Mode != SwitchMode.ACITVE)
        {
            throw new Exception(String.Format("Switch {0} cannot be turned on or off because it is of mode {1}", UID, Mode));
        }
        SendCommand(DeviceCommand.OFF);
    }
    #endregion
}
