using System;

namespace Chetch.Arduino.Devices;

public class AnalogSampler : ArduinoDevice
{
    #region Properties
    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 1)] //Because 0 is taken by report interval value
    public UInt16 SampleSize { get; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 2)]
    public UInt16 SampleInterval { get; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.DATA, 0)] //Mean sampled value
    public float MeanValue 
    { 
        get { return meanValue; } 
        internal set
        {
            meanValue = value;
            SamplingComplete?.Invoke(this, meanValue);
        }
    }

    [ArduinoMessageMap(Messaging.MessageType.DATA, 1)] //Mean sampled value
    public UInt16 MinValue { get; internal set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.DATA, 2)] //Mean sampled value
    public UInt16 MaxValue { get; internal set; } = 0;
    #endregion

    #region Events
    public EventHandler<double>? SamplingComplete;
    #endregion

    #region Fields
    float meanValue = 0.0f;
    #endregion

    #region Constructors
    public AnalogSampler(byte id, String sid, String? name = null) : base(id, sid, name)
    {}

    public AnalogSampler(string sid, string? name = null) : base(sid, name)
    {}
    #endregion
}
