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
            ArduinoMessage msg = new ArduinoMessage(message.Type);
            msg.Target = message.Target;
            msg.Sender = message.Sender;
            msg.Tag = message.Tag;
            msg.Add(MCPNode.ReportInterval);
            msg.Add(MCPNode.NodeID);
            foreach (var arg in message.Arguments)
            {
                msg.Add(arg);
            }
            message = msg;
        }

        if (message.Type == Messaging.MessageType.ERROR && message.Target == MCPNode.ID)
        {
            ArduinoMessage msg = new ArduinoMessage(message.Type);
            msg.Target = message.Target;
            msg.Sender = message.Sender;
            msg.Tag = message.Tag;
            msg.Add(ArduinoBoard.ErrorCode.DEVICE_ERROR);
            foreach (var arg in message.Arguments)
            {
                msg.Add(arg);
            }
            message = msg;
        }

        //This will direct the message to the appropriate place
        OnMessageReceived(message);
    }
    #endregion
}