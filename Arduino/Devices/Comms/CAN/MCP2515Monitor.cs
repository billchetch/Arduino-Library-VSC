using System;
using Chetch.Arduino.Boards;
using Chetch.Messaging;

namespace Chetch.Arduino.Devices.Comms.CAN;

public class MCP2515Monitor : MCP2515
{

    #region Constants
    private const byte MESSAGE_ID_FORWARD_RECEIVED = 100;
    private const byte MESSAGE_ID_FORWARD_SENT = 101;

    private const byte MESSAGE_TAG_BUS_MESSAGE = 88;
    #endregion

    #region Classes and Enums
    
    #endregion

    #region Events
    public EventHandler<CANMessage>? BusMessageReceived;

    #endregion

    #region Properties
    #endregion

    #region Fields
    #endregion

    #region Constructors
    public MCP2515Monitor(byte nodeID, string? name = null) : base(nodeID, name)
    {
    }
    #endregion

    #region Methods
    #endregion

    #region Messaging
    override public ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        switch (message.Type)
        {
            //Message of this type are assumed to be 'forwarded' bus messages
            case MessageType.INFO:
                if (message.Tag == MESSAGE_ID_FORWARD_SENT || message.Tag == MESSAGE_ID_FORWARD_RECEIVED)
                {
                    CANID canID = new CANID(message.Get<uint>(1));

                    var busMessage = new CANMessage(canID.NodeID, canID.ID, message.Get<byte[]>(0));
                    busMessage.Message.Type = message.Get<MessageType>(2);
                    busMessage.Message.Tag = canID.Tag;
                    busMessage.Message.Sender = message.Get<byte>(3);
                    busMessage.Message.Target = busMessage.Message.Sender;

                    BusMessageReceived?.Invoke(this, busMessage);
                    
                }
                break;

            case MessageType.COMMAND_RESPONSE:
                if(message.Tag == MESSAGE_TAG_BUS_MESSAGE)
                {
                    bool sendResult = message.Get<bool>(0);
                    //Console.WriteLine("Send result: {0}", sendResult);
                }
                break;
        }

        
        return base.HandleMessage(message);
    }

    protected ArduinoMessage FormulateBusMessage(byte nodeID, ArduinoMessage message)
    {
        if(nodeID == NodeID)
        {
            throw new Exception("Cannot formulate remote node message for the Monitor node as it is not remote!");
        }

        var fmsg = new ArduinoMessage(MessageType.COMMAND);
        fmsg.Tag = MESSAGE_TAG_BUS_MESSAGE;
        fmsg.Target = ID;
        fmsg.Sender = message.Sender;
        switch (message.Type)
        {
            case MessageType.STATUS_REQUEST:
            case MessageType.PING:
            case MessageType.INITIALISE:
            case MessageType.RESET:
            case MessageType.ERROR_TEST:
            case MessageType.FINALISE:
                fmsg.Add(ArduinoDevice.DeviceCommand.REQUEST);
                fmsg.Add(message.Type);
                fmsg.Add(nodeID);
                fmsg.Add(message);
                break;

            case MessageType.COMMAND:
                fmsg.Add(message.Get<ArduinoDevice.DeviceCommand>(0));
                fmsg.Add(nodeID);
                fmsg.Add(message, 1); //add message arguments from 1 onwards (as 0 index arg added above)
                break;

            default:
                throw new Exception(String.Format("Cannot formulate message of type {0}!", message.Type));
        }
        return fmsg;
    }
    
    public ArduinoMessage SendBusMessage(byte nodeID, ArduinoMessage message)
    {
        if(nodeID == NodeID)
        {
            throw new ArgumentException(String.Format("Node {0} is not remote", nodeID));
        }
        var m2s = FormulateBusMessage(nodeID, message);
        SendMessage(m2s);
        UpdateMessageCount(m2s);
        return m2s;
    }

    public ArduinoMessage SendBusMessage(byte nodeID, MessageType messageType)
    {
        return SendBusMessage(nodeID, new ArduinoMessage(messageType));
    }
    #endregion
}