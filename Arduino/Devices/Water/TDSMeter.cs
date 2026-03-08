using System;
using XmppDotNet.Xmpp.MessageEvents;

namespace Chetch.Arduino.Devices.Water;

public class TDSMeter : AnalogSampler
{
    [ArduinoMessageMap(Messaging.MessageType.DATA, 1)] //Mean sampled value
    public float PPM { 
        get{ return ppm; } 
        internal set
        {
            ppm = value;
            PPMAvailable?.Invoke(this, ppm);       
        }
    }

    private float ppm = 0.0f;

    public EventHandler<double>? PPMAvailable;

    public TDSMeter(string sid, string? name = null) : base(sid, name)
    {}
}
