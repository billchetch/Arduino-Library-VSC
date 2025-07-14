using System;

namespace Chetch.Arduino.Devices;

public class TickerGroup : ArduinoDeviceGroup
{
    public event EventHandler<int>? Ticked;

    public TickerGroup(String name) : base(name){}

    public override void Add(ArduinoDevice device)
    {
        if(!(device is Ticker))
        {
            throw new ArgumentException(String.Format("device {0} is not a ticker", device.SID));
        }

        ((Ticker)device).Ticked += handleTicked;
        base.Add(device);
    }

    void handleTicked(Object? sender, int count)
    {
        Ticked?.Invoke(sender, count);
    }
}
