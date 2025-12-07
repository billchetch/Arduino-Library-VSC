using System;
using System.Numerics;
using Chetch.Arduino.Boards;
using Chetch.Messaging;
using Chetch.Utilities;
using Microsoft.EntityFrameworkCore.Storage.Json;
using XmppDotNet.Xmpp.HttpUpload;
using XmppDotNet.Xmpp.Jingle.Candidates;

namespace Chetch.Arduino.Devices.Comms;

abstract public class MCP2515 : ArduinoDevice
{
    #region Constants
    public const int ERROR_LOG_SIZE = 64;
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
        STALE_MESSAGE, //an old message
        SYNC_ERROR, //if presence is out of sync
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

    public class FlagsChangedEventArgs
    {
        public UInt16 Flags { get; internal set; }
        public UInt16 FlagsChanged { get; internal set; }

        public FlagsChangedEventArgs(UInt16 oldValue, UInt16 newValue)
        {
            FlagsChanged =  (UInt16)(oldValue ^ newValue);
            Flags = newValue;
        }

        public FlagsChangedEventArgs(byte oldValue, byte newValue) : this((UInt16)oldValue, (UInt16)newValue)
        { }
    }
    #endregion

    #region Properties
    public MCP2515ErrorCode LastError => (MCP2515ErrorCode)Error;

    [ArduinoMessageMap(Messaging.MessageType.ERROR, 2)]
    public UInt32 LastErrorData { get; internal set; } = 0;

    public DateTime LastErrorOn { get; internal set; }

    public String ErrorSummary => String.Format("{0}: {1} ({2})", LastErrorOn.ToString("s"), LastError, Chetch.Utilities.Convert.ToBitString(LastErrorData, "-"));
    
    public Dictionary<MCP2515ErrorCode, uint> ErrorCounts { get; } = new Dictionary<MCP2515ErrorCode, uint>();

    public RingBuffer<String> ErrorLog { get; } = new RingBuffer<String>(ERROR_LOG_SIZE, true);

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 1)] //Start at 1 as 0 is for ReportInterval
    public byte NodeID { get; internal set; } //Default is 1 as this is the normal bus master node ID

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 2)]
    [ArduinoMessageMap(Messaging.MessageType.PRESENCE, 3)]
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
    [ArduinoMessageMap(Messaging.MessageType.ERROR, 4)]
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

    [ArduinoMessageMap(Messaging.MessageType.ERROR, 3)]
    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 6)]
    public UInt16 ErrorCodeFlags
    {
        get { return errorCodeFlags; }
        internal set
        {
            if (value != errorCodeFlags)
            {
                ErrorCodeFlagsChanged?.Invoke(this, new FlagsChangedEventArgs(errorCodeFlags,  value));
            }
            errorCodeFlags = value;
        }
    }

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 7)]
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

    public bool Initialised { get; internal set; } = false; //Set on receiving an INITIALISE_RESPONSE message
    

    [ArduinoMessageMap(Messaging.MessageType.PRESENCE, 0)]
    [ArduinoMessageMap(Messaging.MessageType.PING_RESPONSE, 0)]
    [ArduinoMessageMap(Messaging.MessageType.INITIALISE_RESPONSE, 0)]
    public UInt32 NodeMillis 
    { 
        get
        {
            return nodeMillis;
        } 
        internal set
        {
            if(nodeMillisSetOn != default(DateTime))
            {
                UInt32 localInterval = (UInt32)(DateTime.Now - nodeMillisSetOn).TotalMilliseconds;
                UInt32 expectedValue = nodeMillis + localInterval;
                SyncOffset = (int)(value - expectedValue);
                //Console.WriteLine("N{0}: Local interval: {1}, Expected Value: {2}, Actual Value: {3}, Sync Offset: {4}", NodeID, localInterval, expectedValue, value, SyncOffset);
            }
            nodeMillis = value;
            nodeMillisSetOn = DateTime.Now;
        }
    }

    public int SyncOffset {get; internal set;}  = 0;

    public DateTime LastPresenceOn { get; internal set; }
    #endregion

    #region Events
    public EventHandler<FlagsChangedEventArgs>? StatusFlagsChanged;

    public EventHandler<FlagsChangedEventArgs>? ErrorFlagsChanged;

    public EventHandler<FlagsChangedEventArgs>? ErrorCodeFlagsChanged;

    public EventHandler<bool>? ReadyToSend;
    #endregion

    #region Fields
    private byte statusFlags = 0;
    private byte errorFlags = 0;
    private UInt16 errorCodeFlags = 0;
    private bool canSend = false;
    private UInt32 nodeMillis = 0;

    private DateTime nodeMillisSetOn = default(DateTime);

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

    public bool IsErrorCodeFlagged(MCP2515ErrorCode ecflg)
    {
        return (errorCodeFlags & (int)ecflg) == 1;
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

            case MessageType.INITIALISE_RESPONSE:
                Initialised = true;
                Error = 0;
                LastErrorData = 0;
                LastErrorOn = default(DateTime);
                ErrorCounts.Clear();
                break;

            case MessageType.PRESENCE:
                LastPresenceOn = DateTime.Now;    
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

            LastErrorOn = DateTime.Now;

            ErrorLog.Add(ErrorSummary);
        }
    }

    
    #endregion
}
