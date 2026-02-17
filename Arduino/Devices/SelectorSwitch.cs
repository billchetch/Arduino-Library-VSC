using System;

namespace Chetch.Arduino.Devices;

public class SelectorSwitch : SwitchDevice
{
    #region Events
    public event EventHandler<byte>? Selected;
    #endregion

    #region Constructors
    public SelectorSwitch(byte id, String sid, String? name = null) : base(id, sid, name)
    {
        Switched += (sender, pinState) => {
            if (IsOn && Selected != null)
            {
                Selected.Invoke(this, Pin);
            }
        };
    }
    
    public SelectorSwitch(String sid, String? name = null) : this(0, sid, name)
    {}

    public SelectorSwitch(String sid, SwitchMode mode, String? name = null) : this(sid, name)
    {
        Mode = mode;
    }
    #endregion

    #region Methods
    
    #endregion
}
