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
    public ArduinoBoard? Board { get; set; }

    public byte ID { get; set; } = 0;

    public String Name {get; internal set; } = ArduinoBoard.DEFAULT_NAME;

    public String UID => String.Format("{0}:{1}", Board == null ? "" : Board.Name, Name);

    #endregion

    #region Events
    public EventHandler<ArduinoMessageMap.UpdatedProperties>? Updated;
    #endregion

    #region Constructors
    public ArduinoDevice(String name)
    {
        Name = name;
    }
    #endregion

    #region Messaging

    public virtual ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        //use reflection to read
        var updatedProperties = ArduinoMessageMap.AssignMessageValues(this, message);
        Updated?.Invoke(this, updatedProperties);
        switch(message.Type)
        {
            case MessageType.COMMAND_RESPONSE:
                DeviceCommand originalCommand = message.Get<DeviceCommand>(0);
                break;

            case MessageType.DATA:
                break;
        }


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
        var msg = new ArduinoMessage();
        msg.Type = MessageType.COMMAND;
        msg.Add((byte)command);

        SendMessage(msg);
    }
    #endregion
}