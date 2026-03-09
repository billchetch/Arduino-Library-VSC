using System;

namespace Chetch.Arduino.Devices.Water;

public class FlowMeter : Counter
{
    #region Properties

    [ArduinoMessageMap(Messaging.MessageType.DATA, 2)] //Mean sampled value    
    public float FlowRate {get; internal set; }

    public float LPM => FlowRate * 0.06f; //liters per minute

    public float LPS => FlowRate / 1000.0f; //liters per minute
    #endregion

    #region Events
    public EventHandler<float>? FlowRateAvailable;
    #endregion

    #region Constructors
    public FlowMeter(string sid, string? name = null) : base(sid, name)
    {}
    #endregion

    #region Messaging
    protected override void OnCountUpdated()
    {
        base.OnCountUpdated();
        FlowRateAvailable?.Invoke(this, FlowRate);
    }
    #endregion
}
