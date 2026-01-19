using System;
using Chetch.Arduino.Devices.Comms;

namespace Chetch.Arduino.Boards;

public class CANBusNode : ArduinoBoard, ICANBusNode
{
    #region Constants and Statics
    
    #endregion

    #region Properties
    public MCP2515Node MCPNode { get; set; }

    public MCP2515 MCPDevice => MCPNode; // for interface compliance

    public CANNodeState NodeState => MCPDevice.State;
    #endregion

    #region Constructors
    public CANBusNode(MCP2515Node mcpNode, String sid) : base(sid)
    {
        MCPNode = mcpNode;
        
        AddDevice(MCPNode);
    }

    public CANBusNode(byte nodeID, String sid) : this(new MCP2515Node(nodeID), sid)
    {}
    
    public CANBusNode(byte nodeID) : this(nodeID, "canbusnode" + nodeID) 
    {}
    #endregion

    #region Methods
    #endregion

    #region Messaging
    public override bool RouteMessage(ArduinoMessage message)
    {
        MCPNode.UpdateMessageCount();
            
        return base.RouteMessage(message);
    } 
    #endregion
}