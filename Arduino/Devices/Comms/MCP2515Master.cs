using System;
using Chetch.Messaging;

namespace Chetch.Arduino.Devices.Comms;

public class MCP2515Master : MCP2515
{

    #region Constants
    private const byte MESSAGE_ID_FORWARD_RECEIVED = 100;
    private const byte MESSAGE_ID_FORWARD_SENT = 101;
    #endregion

    #region Classes and Enums
    public enum BusMessageDirection
    {
        OUTBOUND,
        INBOUND
    }

    public class BusMessageEventArgs
    {
        public CANID CanID { get; internal set; }

        public byte NodeID => CanID.NodeID;

        public byte CanDLC => (byte)CanData.Length;

        public byte[] CanData { get; }

        public ArduinoMessage Message { get; } = new ArduinoMessage();

        public BusMessageDirection Direction { get; internal set; }

        public BusMessageEventArgs(ArduinoMessage message)
        {
            if (message.Tag == MESSAGE_ID_FORWARD_SENT)
            {
                Direction = BusMessageDirection.OUTBOUND;
            }
            else if (message.Tag == MESSAGE_ID_FORWARD_RECEIVED)
            {
                Direction = BusMessageDirection.INBOUND;
            }

            CanData = message.Get<byte[]>(0);
            CanID = new CANID(message.Get<UInt32>(1));
            Message.Type = message.Get<MessageType>(2);
            Message.Sender = message.Get<byte>(3);
            Message.Target = Message.Sender;
            Message.Tag = CanID.Tag;
        }
    }
    
    public class BusActivityEventArgs
    {
        public BusActivityEventArgs()
        {
            
        }
    }
    #endregion

    #region Events
    public EventHandler<BusMessageEventArgs>? BusMessageReceived;

    public EventHandler? BusActivityUpdated;

    #endregion

    #region Properties
    [ArduinoMessageMap(MessageType.DATA, 0)]     
    public UInt16 BusMessageCount{ 
        get => busMessageCount;
        set
        {
            busMessageCount = value;
            OnUpdateBusActivity();
        } 
    }
    
    #endregion

    #region Fields
    private UInt16 busMessageCount = 0;
    #endregion

    #region Constructors
    public MCP2515Master(byte nodeID = 1, string? name = null) : base(nodeID, name)
    {}
    #endregion

    #region Methods
    protected void OnUpdateBusActivity()
    {
        BusActivityUpdated?.Invoke(this, EventArgs.Empty);
    }
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
                    var eargs = new BusMessageEventArgs(message);
                    if(eargs.NodeID == NodeID)
                    {
                        UpdateMessageCount();
                    }
                    BusMessageReceived?.Invoke(this, eargs);
                }
                break;
        }

        
        return base.HandleMessage(message);
    }

    protected ArduinoMessage FormulateBusMessage(byte nodeID, ArduinoMessage message)
    {
        if(nodeID == NodeID)
        {
            throw new Exception("Cannot formulate remote node message for the Master node as it is not remote!");
        }

        var fmsg = new ArduinoMessage(MessageType.COMMAND);
        fmsg.Target = ID;
        fmsg.Sender = message.Sender;
        switch (message.Type)
        {
            case MessageType.STATUS_REQUEST:
            case MessageType.PING:
            case MessageType.INITIALISE:
            case MessageType.RESET:
            case MessageType.ERROR_TEST:
                fmsg.Add(ArduinoDevice.DeviceCommand.REQUEST);
                fmsg.Add(message.Type);
                fmsg.Add(nodeID);
                fmsg.Add(message);
                break;

            case MessageType.COMMAND:
                fmsg.Add(message.Get<ArduinoDevice.DeviceCommand>(0));
                fmsg.Add(nodeID);
                fmsg.Add(message, 1);
                break;

            default:
                throw new Exception("Cannot formulate this message!");
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
        UpdateMessageCount();
        
        return m2s;
    }

    public ArduinoMessage SendBusMessage(byte nodeID, MessageType messageType)
    {
        return SendBusMessage(nodeID, new ArduinoMessage(messageType));
    }
    #endregion
}