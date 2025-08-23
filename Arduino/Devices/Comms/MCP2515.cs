using System;
using Chetch.Messaging;
using XmppDotNet.Xmpp.HttpUpload;

namespace Chetch.Arduino.Devices.Comms;

public class MCP2515 : ArduinoDevice
{
    #region Constants
    private const byte MESSAGE_ID_FORWARD_RECEIVED = 100;
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

    public class CANID
    {
        public UInt32 ID { get; internal set; } = 0;

        public CANMessagePriority Priority => (CANMessagePriority)(ID >> 24 & 0x0F);

        public MessageType Messagetype => (MessageType)((ID >> 16) & 0xFF);

        public byte NodeID => (byte)(ID >> 12 & 0x0F);

        public byte Sender => (byte)(ID >> 8 & 0x0F);

        public byte MessageStructure => (byte)(ID & 0xFF);

        public CANID(UInt32 canId)
        {
            ID = canId;
        }
    }

    public class ForwardedMessageEventArgs
    {

        public CANID CanID { get; internal set; }

        public byte CanDLC { get; internal set; } = 0;

        public List<byte> CanData { get; } = new List<byte>();

        public ArduinoMessage Message { get; } = new ArduinoMessage();


        public ForwardedMessageEventArgs(ArduinoMessage message)
        {
            Message.Sender = message.Sender;
            Message.Target = message.Target;
            Message.Tag = message.Tag;
            int argCount = message.Arguments.Count;

            //Last 3 arguments of the message forwarded are 'meta' data which we extract
            CanID = new CANID(message.Get<UInt32>(argCount - 3)); //last but two
            CanDLC = message.Get<byte>(argCount - 2); //last but one
            Message.Type = message.Get<MessageType>(argCount - 1); //last argument
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
    #endregion

    #region Properties
    public MCP2515ErrorCode LastError => (MCP2515ErrorCode)Error;

    public Dictionary<MCP2515ErrorCode, UInt32> ErrorCounts { get; } = new Dictionary<MCP2515ErrorCode, UInt32>();
    
    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 1)]
    public bool CanReceiveMessages { get; internal set; } = false;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 2)]
    public bool CanReceiveErrors { get; internal set; } = false;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 3)]
    public byte NodeID { get; internal set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 4)]
    [ArduinoMessageMap(Messaging.MessageType.DATA, 0)]
    public byte StatusFlags { get; internal set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 5)]
    [ArduinoMessageMap(Messaging.MessageType.DATA, 1)]
    [ArduinoMessageMap(Messaging.MessageType.ERROR, 2)]
    public byte ErrorFlags { get; internal set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 6)]
    [ArduinoMessageMap(Messaging.MessageType.DATA, 2)]
    [ArduinoMessageMap(Messaging.MessageType.ERROR, 3)]
    public byte TXErrorCount { get; internal set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 7)]
    [ArduinoMessageMap(Messaging.MessageType.DATA, 3)]
    [ArduinoMessageMap(Messaging.MessageType.ERROR, 4)]
    public byte RXErrorCount { get; internal set; } = 0;

    public UInt32 ForwardedReceivedCount { get; internal set; } = 0;
    #endregion

    #region Events
    public EventHandler<ForwardedMessageEventArgs>? MessageForwarded;
    #endregion

    #region Constructors
    public MCP2515(string sid, string? name = null) : base(sid, name)
    {
        var enumValues = Enum.GetValues(typeof(MCP2515ErrorCode));
        foreach (int val in enumValues) {
            ErrorCounts[(MCP2515ErrorCode)val] = 0;
        }
    }
    #endregion

    #region Messaging
    protected override void OnError(ArduinoMessage message)
    {
        base.OnError(message);

        ErrorCounts[LastError]++;
    }

    override public ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        switch (message.Type)
        {
            //Message of this type are assumed to be 'forwarded' messages
            case MessageType.INFO:
                //Seperate messages
                if (message.Tag == MESSAGE_ID_FORWARD_RECEIVED)
                {
                    ForwardedReceivedCount++;
                }
                MessageForwarded?.Invoke(this, new ForwardedMessageEventArgs(message));
                break;
        }
        return base.HandleMessage(message);
    }
    #endregion
}
