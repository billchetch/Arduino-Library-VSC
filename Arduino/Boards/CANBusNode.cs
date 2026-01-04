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

    public IEnumerable<MCP2515.ErrorLogEntry> ErrorLog => MCPNode.ErrorLog;

    override public bool IsReady => NodeID != MASTER_NODE_ID ? true : base.IsReady;
    
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

    public CANBusNode(byte nodeID) : this(nodeID, "canbusnode" + nodeID) { }

    public CANBusNode() : this(0, "canbusnode") { }
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
            switch (message.Type)
            {
                case Messaging.MessageType.STATUS_RESPONSE:
                    //Status Flags, Error Flags, errorCountTX, errorCountRX, errorCountFlags
                    message.Populate<byte, byte, byte, byte, UInt16>(canData);
                    message.Add(MCPNode.ReportInterval, 0);
                    message.Add(MCPNode.NodeID, 1);
                    break;

                case Messaging.MessageType.INITIALISE_RESPONSE:
                    //Millis and timestamp resolution
                    message.Populate<UInt32, byte>(canData);
                    break;

                case Messaging.MessageType.ERROR:
                    //Error Code, Error Data, Error Code Flags, MCP Error Flags
                    message.Populate<byte, UInt32, UInt16, byte>(canData);
                    message.Add(ArduinoBoard.ErrorCode.DEVICE_ERROR, 0);
                    break;

                case Messaging.MessageType.PRESENCE:
                    //Nodemillis, Interval, Initial presence, Status Flags
                    message.Populate<UInt32, UInt16, bool, byte>(canData);
                    break;
            }
            
            //This will direct the message to the appropriate place
            OnMessageReceived(message);
        }
    }
    #endregion
}