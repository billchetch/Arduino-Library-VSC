using System;
using Chetch.Messaging;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Chetch.Arduino;

abstract public class ArduinoDevice
{
    #region Properties
    public byte ID { get; set; } = 0;

    public String Name {get; internal set; } = ArduinoBoard.DEFAULT_NAME;
    #endregion

    public ArduinoDevice(String name)
    {
        Name = name;
    }

    #region Messaging

    public virtual void HandleMessage(ArduinoMessage message)
    {
        //use reflection to read
        ArduinoMessageMap.AssignMessageValues(this, message);
    }
    #endregion
}