using System;
using Chetch.Messaging;

namespace Chetch.Arduino.Devices.Comms.Serial;

abstract public class SerialPin : ArduinoDevice
{
    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 1)] //Start at 1 as 0 is for ReportInterval
    public byte Pin { get; internal set; }   

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 2)] //Start at 1 as 0 is for ReportInterval
    public Int16 Interval { get; internal set; }   

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 3)] //Start at 1 as 0 is for ReportInterval
    public byte BufferSize { get; internal set; }   

    public SerialPin(string sid, string? name = null) : base(sid, name)
    {
    }
}
