using System;

namespace Chetch.Arduino.Devices.Comms;

public class MCP2515 : ArduinoDevice
{

    #region Properties
    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 1)]
    public bool CanReceiveMessages { get; internal set; } = false;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 2)]
    public bool CanReceiveErrors { get; internal set; } = false;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 3)]
    public byte StatusFlags { get; internal set; } = 0;
    #endregion


    #region Constructors
    public MCP2515(string sid, string? name = null) : base(sid, name)
    {

    }
    #endregion

    #region Messaging
    override public ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        return base.HandleMessage(message);
    }
    #endregion
}
