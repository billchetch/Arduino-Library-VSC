using System;
using Chetch.Arduino.Devices.Comms;

namespace Chetch.Arduino.Boards;

public class CANBusNode : ArduinoBoard
{
    #region Constants and Statics
    public const byte MASTER_NODE_ID = 1;
    
    #endregion

    #region Properties
    public MCP2515 MCPNode { get; }

    public byte NodeID => MCPNode.NodeID;

    override public bool IsReady => NodeID != MASTER_NODE_ID ? true : base.IsReady;
    public uint BusMessageCount { get; internal set; } = 0;

    #endregion

    #region Constructors
    public CANBusNode(byte nodeID, String sid) : base(sid)
    {
        if(nodeID == MASTER_NODE_ID)
        {
            throw new ArgumentException(String.Format("Node ID {0} is not valid", nodeID)); 
        }
        MCPNode = new MCP2515Node(nodeID);

        AddDevice(MCPNode);
    }
    
    public CANBusNode(MCP2515 mcpNode, String sid) : base(sid)
    {
        MCPNode = mcpNode;
        
        AddDevice(MCPNode);
    }

    public CANBusNode(byte nodeID) : this(nodeID, "mcp" + nodeID) { }

    public CANBusNode() : this(0, "mcp") { }
    #endregion

    #region Methods
    public void SetNodeID(byte nodeID)
    {
        MCPNode.SetNodeID(nodeID);
    }
    #endregion

    #region Messaging
    public virtual void HandleBusMessage(CANID canID, byte[] canData, ArduinoMessage message)
    {
        bool onTarget = message.Target == MCPNode.ID;
        if (onTarget)
        {
            BusMessageCount++;
            
            switch (message.Type)
            {
                case Messaging.MessageType.STATUS_RESPONSE:
                    //Status Flags, Error Flags, errorCountTX, errorCountRX
                    message.Populate<byte, byte, byte, byte>(canData);
                    message.Add(MCPNode.ReportInterval, 0);
                    message.Add(MCPNode.NodeID, 1);
                    break;

                case Messaging.MessageType.ERROR:
                    message.Populate<byte, UInt32, byte, byte>(canData);
                    message.Add(ArduinoBoard.ErrorCode.DEVICE_ERROR, 0);
                    break;

                case Messaging.MessageType.PRESENCE:
                    message.Populate<UInt32, UInt16, bool, byte>(canData);
                    break;
            }
            
            //This will direct the message to the appropriate place
            OnMessageReceived(message);
        }
    }
    #endregion
}