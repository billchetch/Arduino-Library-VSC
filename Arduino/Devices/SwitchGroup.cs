using System;

namespace Chetch.Arduino.Devices;

public class SwitchGroup : ArduinoDeviceGroup
{
    public class EventArgs
    {
        public SwitchDevice Switch;
        public bool PinState = false;

        public EventArgs(SwitchDevice device, bool pinState)
        {
            Switch = device;
            PinState = pinState;
        }
    }

    public event EventHandler<EventArgs>? Switched;

    public override void Add(ArduinoDevice device)
    {
        if(!(device is SwitchDevice))
        {
            throw new ArgumentException(String.Format("device {0} is not a switch", device.Name));
        }

        ((SwitchDevice)device).Switched += handleSwitched;
        base.Add(device);
    }

    void handleSwitched(Object? sender, bool pinState){
        if(sender != null)
        {
            Switched?.Invoke(this, new EventArgs((SwitchDevice)sender, pinState));
        }
    }

    public void TurnOff()
    {
        foreach(var dev in this)
        {
            ((SwitchDevice)dev).TurnOff();
        }
    }

    public void TurnOn()
    {
        foreach(var dev in this)
        {
            ((SwitchDevice)dev).TurnOn();
        }
    }
}
