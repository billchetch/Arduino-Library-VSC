using System;
using Chetch.Arduino.Boards;
using Chetch.Messaging;
using XmppDotNet.Xmpp.HttpUpload;

namespace Chetch.Arduino.Devices.Comms;

public class MCP2515 : ArduinoDevice
{
    #region Constants
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
        READ_FAIL,
        CRC_ERROR,
        CUSTOM_ERROR,
        DEBUG_ASSERT
    };

    /*
    STATUS FLAGS (Bits in the byte read from the ? register)
    Source: MCP2515-Stand-Alone-CAN-Controller-with-SPI-20001801J.pdf)
    D7: TX2IF (CANINTF[4]) Transmit Buffer-2-Empty Interrupt Flag bit
    D6: TXREQ (TXB2CNTRL[3]) Buffer 2, Message-Transmit-Request bit
    D5: TX1IF (CANINTF[3]) Transmit Buffer-1-Empty Interrupt Flag bit
    D4: TXREQ (TXB1CNTRL[3]) Buffer 1, Message-Transmit-Request bit
    D3: TX0IF (CANINTF[2]) Transmit Buffer-0-Empty Interrupt Flag bit
    D2: TXREQ (TXB0CNTRL[3]) Buffer 0, Message-Transmit-Request bit
    D1: RX1IF (CANINTF[1]) Receive-Buffer-1-Full Interrupt Flag
    D0: RX0IF (CANINTF[0]) Receive-Buffer-0-Full Interrupt Flag
    */
    public enum CANStatusFlag
    {
        SFLG_TX2EMPTY = (1 << 7),
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

    public class BusMessageEventArgs
    {
        public CANID CanID { get; internal set; }

        public byte NodeID => CanID.NodeID;

        public byte CanDLC { get; internal set; } = 0;

        public List<byte> CanData { get; } = new List<byte>();

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

    public Dictionary<MCP2515ErrorCode, uint> ErrorCounts { get; } = new Dictionary<MCP2515ErrorCode, uint>();

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 1)] //Start at 1 as 0 is for ReportInterval
    public byte NodeID { get; internal set; } //Default is 1 as this is the normal bus master node ID

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
    [ArduinoMessageMap(Messaging.MessageType.ERROR, 3)]
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
    public byte TXErrorCount { get; internal set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 5)]
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

    override public bool StatusRequested => base.StatusRequested || NodeID != CANBusNode.MASTER_NODE_ID;
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
    public MCP2515(byte nodeID, string? name = null) : base("mcp" + nodeID, name)
    {
        NodeID = nodeID;
    }
    #endregion

    #region Methods
    public void SetNodeID(byte nodeID)
    {
        NodeID = nodeID;
    }

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
                }
                break;

            //Message of this type are assumed to be 'forwarded' bus messages
            case MessageType.INFO:
                if (message.Tag == MESSAGE_ID_FORWARD_SENT || message.Tag == MESSAGE_ID_FORWARD_RECEIVED)
                {
                    var eargs = new BusMessageEventArgs(message);
                    BusMessageReceived?.Invoke(this, eargs);
                }
                break;
        }
        return base.HandleMessage(message);
    }

    override protected void OnError(ArduinoMessage message)
    {
        base.OnError(message);

        if(LastError != MCP2515ErrorCode.NO_ERROR)
        {
            if(!ErrorCounts.ContainsKey(LastError))
            {
                ErrorCounts[LastError] = 0;
            }
            ErrorCounts[LastError]++;
        }
    }

    public void RequestRemoteNodesStatus()
    {
        SendCommand(DeviceCommand.REQUEST);
    }

    public void SynchroniseBus()
    {
        SendCommand(DeviceCommand.SYNCHRONISE);
    }
    #endregion
}
