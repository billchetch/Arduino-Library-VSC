using System;

namespace Chetch.Arduino.Devices.Comms;

public class SerialPinSlave : SerialPin
{
    #region Constants
    public const String SPIN_SLAVE_SID = "spinm";
    #endregion

    public SerialPinSlave(string sid, string? name = null) : base(sid, name)
    {
    }

    public SerialPinSlave(String? name = null) : this(SPIN_SLAVE_SID, name)
    {
    }
}
