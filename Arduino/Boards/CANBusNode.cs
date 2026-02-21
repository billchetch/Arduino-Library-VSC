using System;
using Chetch.Arduino.Devices.Comms.Serial;
using Chetch.Arduino.Devices.Comms.CAN;

namespace Chetch.Arduino.Boards;

public class CANBusNode : ArduinoBoard, ICANBusNode
{
    #region Constants and Statics
    
    #endregion

    #region Properties
    public byte NodeID => MCPNode.NodeID;

    public ICANDevice CANDevice => MCPNode;
    
    protected MCP2515Node MCPNode { get; set; }

    public SerialPinSlave SerialPin { get; } = new SerialPinSlave();

    #endregion

    #region Constructors
    public CANBusNode(MCP2515Node mcpNode, String sid) : base(sid)
    {
        MCPNode = mcpNode;

        AddDevice(MCPNode);

        AddDevice(SerialPin);
    }

    public CANBusNode(byte nodeID, String sid) : this(new MCP2515Node(nodeID), sid)
    {}
    
    public CANBusNode(byte nodeID) : this(nodeID, "canbusnode" + nodeID) 
    {}
    #endregion

    #region Methods
    #endregion

    #region Messaging
    override public bool RouteMessage(ArduinoMessage message)
    {
        MCPNode.UpdateMessageCount(message);
        return base.RouteMessage(message);
    }
    #endregion
}