using System;

namespace Chetch.Arduino.Devices;

public class Ticker : ArduinoDevice
{
    #region Properties
    [ArduinoMessageMap(Messaging.MessageType.DATA, 0)]
    public int Count
    {
        get
        {
            return count;
        }
        internal set
        {
            bool changed = count != value;
            count = value;
            if (changed)
            {
                Ticked?.Invoke(this, count);
            }
        }
    }
    #endregion

    #region Events
    public event EventHandler<int>? Ticked;
    #endregion

    #region Fields
    int count = -1;
    #endregion

    public Ticker(byte id, String sid) : base(id, sid) { }
    public Ticker(string sid) : this(0, sid){}
}
