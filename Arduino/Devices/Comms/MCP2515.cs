using System;
using Chetch.Messaging;
using XmppDotNet.Xmpp.HttpUpload;

namespace Chetch.Arduino.Devices.Comms;

public class MCP2515 : ArduinoDevice
{
    #region Constants
    public const byte DEFAULT_MASTER_NODE_ID = 1;
    private const byte MESSAGE_ID_FORWARD_RECEIVED = 100;
    private const byte MESSAGE_ID_FORWARD_SENT = 101;
    private const byte MESSAGE_ID_READY_TO_SEND = 102;
    #endregion

    #region Classes and Enums
    public enum MCP2515ErrorCode
    {
        NO_ERROR = 0,
        UNKNOWN_RECEIVE_ERROR,
        UNKNOWN_SEND_ERROR,
        NO_MESSAGE,
        INVALID_MESSAGE,
        FAIL_TX,
        ALL_TX_BUSY,
        READ_FAIL
    };

    public enum CANMessagePriority
    {
        CAN_PRIORITY_RANDOM = 0,
        CAN_PRIORITY_CRITICAL,
        CAN_PRIORITY_HIGH,
        CAN_PRIORITY_NORMAL,
        CAN_PRIORITY_LOW
    };

    public enum CANStatusFlags
    {
        
    }

    public enum CANErrorFlags
    {
        
    }

    public class CANID
    {
        public UInt32 ID { get; internal set; } = 0;

        public CANMessagePriority Priority => (CANMessagePriority)(ID >> 24 & 0x0F);

        public MessageType Messagetype => (MessageType)((ID >> 19) & 0x1F);

        public byte Tag => (byte)((ID >> 16) & 0x03);

        public byte NodeID => (byte)(ID >> 12 & 0x0F);

        public byte Sender => (byte)(ID >> 8 & 0x0F);

        public byte MessageStructure => (byte)(ID & 0xFF);

        public CANID(UInt32 canId)
        {
            ID = canId;
        }
    }

    public class BusMessageEventArgs
    {
        public CANID CanID { get; internal set; }

        public byte CanDLC { get; internal set; } = 0;

        public List<byte> CanData { get; } = new List<byte>();

        public ArduinoMessage Message { get; } = new ArduinoMessage();


        public BusMessageEventArgs(ArduinoMessage message)
        {
            Message.Sender = message.Sender;
            Message.Target = message.Target;
            int argCount = message.Arguments.Count;

            //Last 3 arguments of the message forwarded are 'meta' data which we extract
            CanID = new CANID(message.Get<UInt32>(argCount - 3)); //last but two
            CanDLC = message.Get<byte>(argCount - 2); //last but one
            Message.Type = message.Get<MessageType>(argCount - 1); //last argument
            Message.Tag = CanID.Tag;
            
            for (int i = 0; i < argCount - 3; i++)
            {
                byte[]? bytes = message.Arguments[i];
                if (bytes != null)
                {
                    Message.Add(bytes);
                    CanData.AddRange(bytes);
                }
            }
        }

        public void Validate()
        {
            //TODO: throws some exceptions when called to facilitate logging
        }
    }

    public class FlagsChangedEventArgs
    {
        public byte Flags { get; internal set; } = 0;
        public byte FlagsChanged { get; internal set; } = 0;
        
        public FlagsChangedEventArgs(byte oldValue, byte newValue)
        {
            FlagsChanged = (byte)(oldValue ^ newValue);
            Flags = newValue;
        }
    }
    #endregion

    #region Properties
    public MCP2515ErrorCode LastError => (MCP2515ErrorCode)Error;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 1)]
    public bool CanReceiveMessages { get; internal set; } = false;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 2)]
    public bool CanReceiveErrors { get; internal set; } = false;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 3)]
    public byte NodeID { get; internal set; } = DEFAULT_MASTER_NODE_ID; //Default is 1 as this is the normal bus master node ID

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 4)]
    public byte StatusFlags
    {
        get { return statusFlags; }
        internal set
        {
            if (value != statusFlags)
            {
                StatusFlagsChanged?.Invoke(this, new FlagsChangedEventArgs(statusFlags, value));
            }
            statusFlags = value;
        }
    }

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 5)]
    [ArduinoMessageMap(Messaging.MessageType.ERROR, 2)]
    public byte ErrorFlags
    {
        get { return errorFlags; }
        internal set
        {
            if (value != errorFlags)
            {
                ErrorFlagsChanged?.Invoke(this, new FlagsChangedEventArgs(errorFlags, value));
            }
            errorFlags = value;
        }
    }

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 6)]
    [ArduinoMessageMap(Messaging.MessageType.ERROR, 3)]
    public byte TXErrorCount { get; internal set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 7)]
    [ArduinoMessageMap(Messaging.MessageType.ERROR, 4)]
    public byte RXErrorCount { get; internal set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 8)]
    public bool CanSend
    {
        get { return canSend; }
        internal set
        {
            if (value != canSend)
            {
                ReadyToSend?.Invoke(this, value);
            }
            canSend = value;
        }
    }
    public UInt32 BusMessageTXCount { get; internal set; } = 0;
    public UInt32 BusMessageRXCount { get; internal set; } = 0;
    
    #endregion

    #region Events
    public EventHandler<BusMessageEventArgs>? BusMessageReceived;

    public EventHandler<FlagsChangedEventArgs>? StatusFlagsChanged;

    public EventHandler<FlagsChangedEventArgs>? ErrorFlagsChanged;

    public EventHandler<bool>? ReadyToSend;
    #endregion

    #region Fields
    private byte statusFlags = 0;
    private byte errorFlags = 0;
    private bool canSend = false;
    #endregion

    #region Constructors
    public MCP2515(string sid, string? name = null) : base(sid, name)
    {
        //Empty
    }
    #endregion

    #region Messaging
    override public ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        switch (message.Type)
        {
            case MessageType.NOTIFICATION:
                if (message.Tag == MESSAGE_ID_READY_TO_SEND)
                {
                    ReadyToSend?.Invoke(this, true);
                }
                break;

            //Message of this type are assumed to be 'forwarded' bus messages
            case MessageType.INFO:
                if (message.Tag == MESSAGE_ID_FORWARD_SENT)
                {
                    BusMessageTXCount++;
                }
                else if (message.Tag == MESSAGE_ID_FORWARD_RECEIVED)
                {
                    BusMessageRXCount++;
                }
                BusMessageReceived?.Invoke(this, new BusMessageEventArgs(message));
                break;
        }
        return base.HandleMessage(message);
    }

    public void RequestNodesStatus()
    {
        SendCommand(DeviceCommand.REQUEST);
    }
    #endregion
}
