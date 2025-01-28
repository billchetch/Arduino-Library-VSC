using System;
using Chetch.Messaging;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Chetch.Arduino;

abstract public class ArduinoDevice : IMessageUpdatableObject
{
    #region Properties
    public ArduinoBoard? Board { get; set; }

    public byte ID { get; set; } = 0;

    public String Name {get; internal set; } = ArduinoBoard.DEFAULT_NAME;

    #endregion

    public ArduinoDevice(String name)
    {
        Name = name;
    }

    #region Messaging

    public virtual ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        //use reflection to read
        return ArduinoMessageMap.AssignMessageValues(this, message);
    }
    #endregion
}