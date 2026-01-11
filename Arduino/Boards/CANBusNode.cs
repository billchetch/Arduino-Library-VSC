using System;
using Chetch.Arduino.Devices.Comms;

namespace Chetch.Arduino.Boards;

public class CANBusNode : ArduinoBoard, ICANBusNode
{
    #region Constants and Statics
    
    #endregion

    #region Properties
    public MCP2515 MCPNode { get; set; }

    public byte NodeID => MCPNode.NodeID;

    public IEnumerable<MCP2515.ErrorLogEntry> ErrorLog => MCPNode.ErrorLog;

    
    #endregion

    #region Constructors
    public CANBusNode(MCP2515 mcpNode, String sid) : base(sid)
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
   
    #endregion
}