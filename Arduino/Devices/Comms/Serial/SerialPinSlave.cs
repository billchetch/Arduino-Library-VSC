using System;

namespace Chetch.Arduino.Devices.Comms.Serial;

public class SerialPinSlave : SerialPin
{
    #region Constants
    public const String SPIN_SLAVE_SID = "spinm";
    #endregion

    #region Events
    public event EventHandler<byte[]>? DataReceived;
    #endregion

    public SerialPinSlave(string sid = SPIN_SLAVE_SID, string? name = null) : base(sid, name)
    {
    }

    #region Messaging
    public override ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        switch (message.Type)
        {
            case Messaging.MessageType.DATA:
                if(message.Arguments.Count > 0){
#pragma warning disable CS8604 // Possible null reference argument.
                    DataReceived?.Invoke(this, message.Arguments[0]);
#pragma warning restore CS8604 // Possible null reference argument.
                }
                break;
        }
        return base.HandleMessage(message);
    }
    #endregion
}
