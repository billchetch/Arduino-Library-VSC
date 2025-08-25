using System;
using Chetch.Messaging;
using XmppDotNet.Xmpp.HttpUpload;

namespace Chetch.Arduino.Devices.Comms;

public class MCP2515 : ArduinoDevice
{
    #region Constants
    public const byte MASTER_NODE_ID = 1;
    private const byte MESSAGE_ID_FORWARD_RECEIVED = 100;
    private const byte MESSAGE_ID_FORWARD_SENT = 101;
    private const byte MESSAGE_ID_READY_TO_SEND = 102;
    private const byte MESSAGE_ID_REPORT_ERROR = 110;
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

    /*
    STATUS FLAGS
    D7: Transmit Buffer-2-Empty Interrupt Flag bit
    D6: Buffer 2, Message-Transmit-Request bit
    D5: Transmit Buffer-1-Empty Interrupt Flag bit
    D4: Buffer 1, Message-Transmit-Request bit
    D3: Transmit Buffer-0-Empty Interrupt Flag bit
    D2: Buffer 0, Message-Transmit-Request bit
    D1: Receive-Buffer-1-Full Interrupt Flag
    D0: Receive-Buffer-0-Full Interrupt Flag
    */
    public enum CANStatusFlag
    {
        SFLG_TX2EMPTY= (1 << 7),
        SFLG_TX2REQUEST = (1 << 6),
        SFLG_TX1EMPTY = (1 << 5),
        SFLG_TX1REQUEST = (1 << 4),
        SFLG_TX0EMPTY = (1 << 3),
        SFLG_TX0REQUEST = (1 << 2),
        SFLG_RX1FULL = (1 << 1),
        SFLG_RX0FULL = (1 << 0)
    }

    /*
    ERROR FLAGS
    D7: RX1OVRF (Receive Buffer 1 Overflow Flag):
    Set when a new valid message is received in Receive Buffer 1, but the buffer is already full.
    D6: RX0OVRF (Receive Buffer 0 Overflow Flag):
    Set when a new valid message is received in Receive Buffer 0, but the buffer is already full.
    D5: TXBO (Bus-Off Flag):
    Set when the Transmit Error Counter (TEC) exceeds 255, indicating that the device has entered a Bus-Off state.
    D4: TXBP (Transmit Error Passive Flag):
    Set when the TEC exceeds 127, indicating that the device has entered an Error Passive state for transmission.
    D3: RXBP (Receive Error Passive Flag):
    Set when the Receive Error Counter (REC) exceeds 127, indicating that the device has entered an Error Passive state for reception.
    D2: TXWAR (Transmit Error Warning Flag):
    Set when the TEC exceeds 96, indicating a warning level for transmit errors.
    D1: RXWAR (Receive Error Warning Flag):
    Set when the REC exceeds 96, indicating a warning level for receive errors.
    D0: EWARN (Error Warning Flag):
    Set when either TXWAR or RXWAR is set, providing a general error warning indication
    */
    public enum CANErrorFlag
    {
        EFLG_RX1OVR = (1 << 7),
        EFLG_RX0OVR = (1 << 6),
        EFLG_TXBO = (1 << 5),
        EFLG_TXEP = (1 << 4),
        EFLG_RXEP = (1 << 3),
        EFLG_TXWAR = (1 << 2),
        EFLG_RXWAR = (1 << 1),
        EFLG_EWARN = (1 << 0)
    }

    public enum BusMessageDirection
    {
        OUTBOUND,
        INBOUND
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

        public BusMessageDirection Direction { get; internal set; }

        public BusMessageEventArgs(ArduinoMessage message, BusMessageDirection direction)
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

            Direction = direction;
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
    public byte NodeID { get; internal set; } = MASTER_NODE_ID; //Default is 1 as this is the normal bus master node ID

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 2)]
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

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 3)]
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

    public bool IsBusOff => IsErrorFlagged(CANErrorFlag.EFLG_TXBO);

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 4)]
    [ArduinoMessageMap(Messaging.MessageType.ERROR, 3)]
    public byte TXErrorCount { get; internal set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 5)]
    [ArduinoMessageMap(Messaging.MessageType.ERROR, 4)]
    public byte RXErrorCount { get; internal set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 6)]
    public bool CanSend
    {
        get { return canSend; }
        internal set
        {
            if (value != canSend)
            {
                try
                {
                    ReadyToSend?.Invoke(this, value);
                }
                catch { }
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

    #region Methods
    public bool IsErrorFlagged(CANErrorFlag eflg)
    {
        return (errorFlags & (int)eflg) == 1;
    }

    public bool IsStatusFlagged(CANStatusFlag sflg)
    {
        return (statusFlags & (int)sflg) == 1;
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
                    CanSend = true;
                    ReadyToSend?.Invoke(this, CanSend);
                }
                break;

            //Message of this type are assumed to be 'forwarded' bus messages
            case MessageType.INFO:
                if (message.Tag == MESSAGE_ID_FORWARD_SENT)
                {
                    BusMessageTXCount++;
                    BusMessageReceived?.Invoke(this, new BusMessageEventArgs(message, BusMessageDirection.OUTBOUND));
                }
                else if (message.Tag == MESSAGE_ID_FORWARD_RECEIVED)
                {
                    BusMessageRXCount++;
                    BusMessageReceived?.Invoke(this, new BusMessageEventArgs(message, BusMessageDirection.INBOUND));
                }
                break;

            case MessageType.ERROR:
                if (message.Tag == MESSAGE_ID_REPORT_ERROR && !IsReady)
                {
                    return new ArduinoMessageMap.UpdatedProperties();
                }
                break;
        }
        return base.HandleMessage(message);
    }

    public void RequestRemoteNodesStatus()
    {
        SendCommand(DeviceCommand.REQUEST);
    }
    #endregion
}
