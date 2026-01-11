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

    override public ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {       
        return base.HandleMessage(message);
    }
}