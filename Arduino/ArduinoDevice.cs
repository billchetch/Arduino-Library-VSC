using System;
using System.Reflection;
using Chetch.Messaging;
using Chetch.Messaging.Attributes;

using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Chetch.Arduino;

abstract public class ArduinoDevice : IMessageUpdatableObject
{
    #region Constants
    public const Int16 DEFAULT_REPORT_INTEVAL = -1;
    #endregion

    #region Classes and Enums
    public enum DeviceCommand : byte
    {
        NONE = 0,
        COMPOUND,
        TEST,
        ENABLE,
        DISABLE,
        SET_REPORT_INTERVAL,
        START,
        STOP,
        PAUSE,
        RESET,
        ON,
        OFF,
        MOVE,
        ROTATE,
        PRINT,
        SET_CURSOR,
        DISPLAY,
        CLEAR,
        SILENCE,
        SEND,
        TRANSMIT,
        SAVE,
        ACTIVATE,
        DEACTIVATE,
        RESUME,
        ZERO,
        ANALYSE,
        LOCK,
        UNLOCK,
        REQUEST,
        SYNCHRONISE,
        UPDATE,
    }
    #endregion

    #region Properties

    [MessageParser(MessageParser.ParsingPolicy.EXCLUDE)]
    public ArduinoBoard? Board
    {
        get
        {
            return board;
        }
        set
        {
            if (board != value)
            {
                board = value;
                if (board == null) return;

                board.Ready += (sender, ready) =>
                {
                    if (ready)
                    {
                        RequestStatus();
                    }
                    else
                    {
                        bool changed = statusResponseReceived;
                        statusRequested = false;
                        statusResponseReceived = false;
                        if (changed)
                        {
                            OnReady(IsReady);
                        }
                    }
                };
            } //end check board has changed (normally from null to a board)
        } //end set method
    }

    [ArduinoMessageMap(MessageType.ERROR, 1)] //Note: Message Index is 1 cos we know that the first index specifies it as a device error
    public byte Error { get; internal set; } = 0;

    //the unique ID for the device on the remote board
    public byte ID { get; set; } = 0;

    //This should be a human readable unique ID
    public String SID {get; internal set; } = ArduinoBoard.DEFAULT_SID;

    public String UID => String.Format("{0}:{1}", Board == null ? "" : Board.SID, SID);

    public String? Name { get; set; } = null;
    
    virtual public bool IsReady => Board != null && Board.IsReady && statusResponseReceived;

    virtual public bool StatusRequested => statusRequested;

    public DateTime LastStatusRequest { get; private set; }

    public DateTime LastStatusResponse { get; private set; }


    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 0)]
    [ArduinoMessageMap(Messaging.MessageType.COMMAND_RESPONSE, 0)]
    public Int16 ReportInterval { get; set; } = DEFAULT_REPORT_INTEVAL;
    #endregion

    #region Fields
    ArduinoBoard? board;
    bool statusRequested = false;
    bool statusResponseReceived = false;
    #endregion

    #region Events
    public event EventHandler<ArduinoMessageMap.UpdatedProperties>? Updated;
    public event EventHandler<bool>? Ready;
    public event EventHandler<ArduinoMessage>? ErrorReceived;
    #endregion

    #region Constructors
    public ArduinoDevice(byte id, String sid, String? name = null)
    {
        ID = id;
        SID = sid;
        Name = name;
    }

    public ArduinoDevice(String sid, String? name = null) : this(0, sid, name)
    {}
    #endregion

    #region Lifecycle
    protected virtual void OnReady(bool ready)
    {
        Ready?.Invoke(this, IsReady);
    }
    #endregion

    #region Messaging
    protected virtual void OnError(ArduinoMessage message)
    {
        ErrorReceived?.Invoke(this, message);
    }

    virtual public bool AssignMessageValue(PropertyInfo propertyInfo, Object propertyValue, ArduinoMessage message)
    {
        switch (message.Type)
        {
            case MessageType.COMMAND_RESPONSE:
                if (propertyInfo.Name == "ReportInterval")
                {
                    var cmd = message.GetLast<DeviceCommand>();
                    if (cmd != DeviceCommand.SET_REPORT_INTERVAL)
                        return false;
                }
                break;
        }

        propertyInfo.SetValue(this, propertyValue);

        return true;
    }
    
    public virtual ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        //use reflection to read
        ArduinoMessageMap.UpdatedProperties updatedProperties;
        switch (message.Type)
        {
            case MessageType.STATUS_RESPONSE:
                bool changed = !statusResponseReceived;
                statusResponseReceived = true;
                LastStatusResponse = DateTime.Now;
                updatedProperties = ArduinoMessageMap.AssignMessageValues(this, message);
                if (changed)
                {
                    OnReady(IsReady);
                }
                break;

            case MessageType.ERROR:
                updatedProperties = ArduinoMessageMap.AssignMessageValues(this, message);
                OnError(message);
                break;

            default:
                updatedProperties = ArduinoMessageMap.AssignMessageValues(this, message);
                break;
        }
        Updated?.Invoke(this, updatedProperties);
        return updatedProperties;
    }

    public void SendMessage(ArduinoMessage message)
    {
        if(Board == null)
        {
            throw new Exception("Cannot send message as Board has no value");
        }

        if(message.Sender == ArduinoMessage.NO_SENDER)
        {
            message.Sender = ID;
        }

        if(message.Target == ArduinoMessage.NO_TARGET)
        {
            message.Target = ID;
        }

        
        Board.SendMessage(message);
    }

    public void SendCommand(DeviceCommand command, params Object[] arguments)
    {
        var msg = new ArduinoMessage(MessageType.COMMAND);
        msg.Add((byte)command);
        foreach (var arg in arguments)
        {
            msg.Add(arg);
        }
        SendMessage(msg);
    }

    public void SetReportInterval(Int16 reportInterval)
    {
        SendCommand(DeviceCommand.SET_REPORT_INTERVAL, reportInterval);
    }

    public void RequestStatus()
    {
        var msg = new ArduinoMessage(MessageType.STATUS_REQUEST);
        //Console.WriteLine("---- Device {0} requesting status...", UID);
        SendMessage(msg);
        statusRequested = true; //flag that this has been requested
        LastStatusRequest = DateTime.Now;
    }

    public void Ping()
    {
        var msg = new ArduinoMessage(MessageType.PING);
        SendMessage(msg);
    }

    public void Initialise()
    {
        var msg = new ArduinoMessage(MessageType.INITIALISE);
        SendMessage(msg);
    }

    public void Reset()
    {
        var msg = new ArduinoMessage(MessageType.RESET);
        SendMessage(msg);
    }

    public void Finalise()
    {
        var msg = new ArduinoMessage(MessageType.FINALISE);
        SendMessage(msg);
    }
    #endregion
}