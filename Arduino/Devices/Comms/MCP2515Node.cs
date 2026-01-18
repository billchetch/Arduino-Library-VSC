using System;

namespace Chetch.Arduino.Devices.Comms;

public class MCP2515Node : MCP2515
{
    #region Properties
    #endregion

    #region Constructors
    public MCP2515Node(byte nodeID, string? name = null) : base(nodeID, name)
    {}
    #endregion

    #region Messaging
    public override ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        UpdateMessageCount();
        
        return base.HandleMessage(message);
    }
    #endregion
}