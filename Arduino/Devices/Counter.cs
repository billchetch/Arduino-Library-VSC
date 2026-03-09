using System;
using XmppDotNet.Xmpp.Client;

namespace Chetch.Arduino.Devices;

public class Counter : ArduinoDevice
{
    #region Properties
    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 1)] //Because 0 is taken by report interval value
    public UInt32 AssignValuesInterval { get; internal set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.DATA, 0)] //Because 0 is taken by report interval value
    public UInt32 Count { get; internal set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.DATA, 1)] //Because 0 is taken by report interval value
    public float Hz { get; internal set; } = 0.0f;

    #endregion

    #region Events
    public EventHandler<UInt32>? CountUpdated;
    #endregion

    #region Constructors
    public Counter(String sid, String? name = null) : base(sid, name)
    {

        Updated += (sender, updatedProps) =>
        {
            bool countUpdated = ContainsUpdatedProperty(updatedProps, "Count", "Hz");
            if (countUpdated)
            {
                CountUpdated?.Invoke(this, Count);
            }
        };
    }
    #endregion
}
