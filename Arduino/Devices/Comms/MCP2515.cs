using System;
using System.Reflection;
using Chetch.Arduino.Boards;
using Chetch.Messaging;
using Chetch.Messaging.Attributes;
using Chetch.Utilities;

namespace Chetch.Arduino.Devices.Comms;

abstract public class MCP2515 : ArduinoDevice
{
    #region Constants
    public const int ERROR_LOG_SIZE = 64;
    #endregion

    #region Classes and Enums
    public enum MCP2515ErrorCode : byte
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

    public enum ResetRegime : byte
    {
        NOT_NOT_SET = 0,
        FULL_RESET = 1,
        CLEAR_ERRORS = 2  
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
    
    public class ErrorLogEntry
    {
        public DateTime EntryDate { get; internal set; } 

        public MCP2515ErrorCode ErrorCode { get; internal set; } = MCP2515ErrorCode.NO_ERROR;

        public UInt32 ErrorData { get; internal set; } = 0;

        public byte ErrorFlags { get; internal set; } = 0;

        public byte StatusFlags { get; internal set; } = 0;

        public String Summary => String.Format("{0}: {1} {2} ({3})", EntryDate.ToString("s"), ErrorCode, Chetch.Utilities.Convert.ToBitString(ErrorFlags), Chetch.Utilities.Convert.ToBitString(ErrorData, "-"));

        public ErrorLogEntry(MCP2515ErrorCode errorCode, UInt32 errorData, byte errorFlags, byte statusFlags)
        {
            EntryDate = DateTime.Now;
            ErrorCode = errorCode;
            ErrorData = errorData;
            ErrorFlags = errorFlags;
            StatusFlags = statusFlags; 
        }
    }
    
    #endregion

    #region Properties
    public MCP2515ErrorCode LastError => (MCP2515ErrorCode)Error;

    [ArduinoMessageMap(Messaging.MessageType.ERROR, 2)]
    public UInt32 LastErrorData { get; internal set; } = 0;

    public DateTime LastErrorOn { get; internal set; }

    [MessageParser(MessageParser.ParsingPolicy.EXCLUDE)]
    public String ErrorSummary => String.Format("{0}: {1} ({2})", LastErrorOn.ToString("s"), LastError, Chetch.Utilities.Convert.ToBitString(LastErrorData, "-"));
    
    public Dictionary<MCP2515ErrorCode, uint> ErrorCounts { get; } = new Dictionary<MCP2515ErrorCode, uint>();

    [MessageParser(MessageParser.ParsingPolicy.EXCLUDE)]
    public CircularLog<ErrorLogEntry> ErrorLog { get; } = new CircularLog<ErrorLogEntry>(ERROR_LOG_SIZE);

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 1)] //Start at 1 as 0 is for ReportInterval
    public byte NodeID { get; internal set; } //Default is 1 as this is the normal bus master node ID

    public CANNodeState State 
    { 
        get => nodeState;
        set
        {
            if(value != nodeState)
            {
                NodeStateChanged?.Invoke(this, value);
            }    
            nodeState = value;
        } 
    } 

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
            nodeMillis = value;
            nodeMillisSetOn = DateTime.Now;
        }
    }

    [MessageParser(MessageParser.ParsingPolicy.EXCLUDE)]
    public UInt32 EstimatedNodeMillis
    {
        get
        {
            if(nodeMillisSetOn != default(DateTime))
            {
                UInt32 localInterval = (UInt32)(DateTime.Now - nodeMillisSetOn).TotalMilliseconds;
                return NodeMillis + localInterval;
            }
            else 
            {
                return 0;
            }
        }
    } 

    [ArduinoMessageMap(Messaging.MessageType.INITIALISE_RESPONSE, 1)]
    public int TimestampResolution { get; internal set; } = -1;

    [ArduinoMessageMap(Messaging.MessageType.INITIALISE_RESPONSE, 2)]
    public int PresenceInterval { get; internal set; } = -1;

    public DateTime LastPresenceOn { get; internal set; }

    public DateTime LastStatusRequest { get; internal set; }

    public DateTime LastStatusResponse { get; internal set; }

    public DateTime LastReadyOn { get; internal set; }

    public uint MessageCount { get; internal set; } = 0;
    
    public double MessageRate { get; internal set; } = -1.0;

    public DateTime LastMessageOn { get; internal set; }

    #endregion

    #region Events
    public EventHandler<CANNodeState>? NodeStateChanged;

    public EventHandler<FlagsChangedEventArgs>? StatusFlagsChanged;

    public EventHandler<FlagsChangedEventArgs>? ErrorFlagsChanged;

    public EventHandler<FlagsChangedEventArgs>? ErrorCodeFlagsChanged;

    #endregion

    #region Fields
    private CANNodeState nodeState = CANNodeState.NOT_SET;

    private byte statusFlags = 0;
    private byte errorFlags = 0;
    private UInt16 errorCodeFlags = 0;
    private UInt32 nodeMillis = 0;
    private DateTime nodeMillisSetOn;
    private uint lastMessageCount = 0;
    private DateTime lastMessageRateUpdated;

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

    public void UpdateMessageCount()
    {
        LastMessageOn = DateTime.Now;
        MessageCount++;
        if(State == CANNodeState.NOT_SET || State == CANNodeState.SILENT)
        {
            State = CANNodeState.TRANSMITTING;
        }
    }

    public double UpdateMessageRate()
    {

        double intervalInSeconds = (DateTime.Now - lastMessageRateUpdated).TotalSeconds;
        MessageRate = (double)(MessageCount - lastMessageCount) / intervalInSeconds;
        lastMessageCount = MessageCount;
        lastMessageRateUpdated = DateTime.Now;

        return MessageRate;
    }
    #endregion

    #region Messaging
    public override bool AssignMessageValue(PropertyInfo propertyInfo, object propertyValue, ArduinoMessage message)
    {
        switch (message.Type)
        {
            case MessageType.ERROR:
                if (propertyInfo.Name == "Error")
                {
                    if (!Enum.IsDefined<MCP2515ErrorCode>((MCP2515ErrorCode)propertyValue))
                    {
                        throw new Exception(String.Format("{0} is not a valid MPC2515Error code", propertyValue));
                    }
                }
                break;
        }
        return base.AssignMessageValue(propertyInfo, propertyValue, message);
    }

    override public ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        switch (message.Type)
        {
            case MessageType.NOTIFICATION:
                break;

            case MessageType.STATUS_RESPONSE:
                LastStatusResponse = DateTime.Now;
                State = CANNodeState.RESPONDING;
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
                bool firstPresence = message.Get<bool>(2);
                if (firstPresence) //signals a node (re)-joining
                {
                    RequestStatus();
                    Initialise();
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

            LastErrorOn = DateTime.Now;

            ErrorLog.Add(new ErrorLogEntry(LastError, LastErrorData, ErrorFlags, StatusFlags));
        }
    }

    protected override void OnReady(bool ready)
    {
        base.OnReady(ready);
        if(ready)
        {
            LastReadyOn = DateTime.Now;
        }
    }

    #endregion
}
