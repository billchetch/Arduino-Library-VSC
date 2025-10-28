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
    #endregion

    #region Constructors
    public CANBusNode(byte nodeID, String sid) : base(sid)
    {
        MCPNode = new MCP2515(nodeID);

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
        if (NodeID != MASTER_NODE_ID && message.Target == MCPNode.ID && message.Type == Messaging.MessageType.STATUS_RESPONSE)
        {
            //Status Flags, Error Flags, errorCountTX, errorCountRX
            message.Populate<byte, byte, byte, byte>(canData);
            message.Add(MCPNode.ReportInterval, 0);
            message.Add(MCPNode.NodeID, 1);
        }

        if (message.Type == Messaging.MessageType.ERROR && message.Target == MCPNode.ID)
        {
            //Error code, Error data, Error Flags, Status Flags
            message.Populate<byte, UInt32, byte, byte>(canData);
            message.Add(ArduinoBoard.ErrorCode.DEVICE_ERROR, 0);
        }

        if (message.Type == Messaging.MessageType.PRESENCE && message.Target == MCPNode.ID)
        {
            message.Populate<UInt32, UInt16, bool, byte>(canData);
        }

        //This will direct the message to the appropriate place
        OnMessageReceived(message);
    }
    #endregion
}