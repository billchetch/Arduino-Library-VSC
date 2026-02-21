using System;

namespace Chetch.Arduino.Devices.Comms.Serial;

public class SerialPinMaster : SerialPin
{
    #region Constants
    public const String SPIN_MASTER_SID = "spinm";
    #endregion

    public SerialPinMaster(string sid, string? name = null) : base(sid, name)
    {
    }

    public SerialPinMaster(String? name = null) : this(SPIN_MASTER_SID, name){}

    public void Send(byte b2s)
    {
        SendCommand(DeviceCommand.SEND, b2s);
    }
}
