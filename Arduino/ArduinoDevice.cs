using System;
using Chetch.Messaging;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Chetch.Arduino;

abstract public class ArduinoDevice : IMessageUpdatableObject
{
    #region Classes and Enums
    public enum DeviceCommand
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
    }
    #endregion

    #region Properties

    public ArduinoBoard? Board 
    { 
        get
        {
            return board;
        } 
        set
        {
            if(board != value)
            {
                board = value;
                if (board == null) return;

                board.Ready += (sender, ready) => {
                    if(ready)
                    {
                        RequestStatus();   
                    }
                    else
                    {
                        bool changed = statusResponseReceived;
                        statusRequested = false;
                        statusResponseReceived = false;
                        if(changed)
                        {
                            OnReady(IsReady);
                        }
                    }
                };
            } //end check board has changed (normally from null to a board)
        } //end set method
    }

    [ArduinoMessageMap(MessageType.ERROR, 1)]
    public byte Error { get; internal set; } = 0;

    //the unique ID for the device on the remote board
    public byte ID { get; set; } = 0;

    //This should be a human readable unique ID
    public String SID {get; internal set; } = ArduinoBoard.DEFAULT_SID;

    public String UID => String.Format("{0}:{1}", Board == null ? "" : Board.SID, SID);

    public String? Name { get; set; } = null;
    
    public bool IsReady => Board != null && Board.IsReady && statusResponseReceived;

    public bool StatusRequested => statusRequested;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 0)]
    public Int16 ReportInterval { get; set; } = -1;
    #endregion

    #region Fields
    ArduinoBoard? board;
    bool statusRequested = false;
    bool statusResponseReceived = false;
    #endregion

    #region Events
    public event EventHandler<ArduinoMessageMap.UpdatedProperties>? Updated;
    public event EventHandler<bool>? Ready;
    #endregion

    #region Constructors
    public ArduinoDevice(byte id, String sid, String? name = null)
    {
        ID = id;
        SID = sid;
        Name = name;
    }

    #endregion

    #region Methods
    public virtual void OnReady(bool ready)
    {
        Ready?.Invoke(this, IsReady);
    }
    #endregion

    #region Messaging

    public virtual ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        //use reflection to read
        ArduinoMessageMap.UpdatedProperties updatedProperties;
        switch(message.Type)
        {
            case MessageType.STATUS_RESPONSE:
                bool changed = !statusResponseReceived;
                statusResponseReceived = true;
                updatedProperties = ArduinoMessageMap.AssignMessageValues(this, message);
                if(changed)
                {
                    OnReady(IsReady);
                }
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

    public void RequestStatus()
    {
        var msg = new ArduinoMessage(MessageType.STATUS_REQUEST);
        SendMessage(msg);
        statusRequested = true; //flag that this has been requested
    }
    #endregion
}