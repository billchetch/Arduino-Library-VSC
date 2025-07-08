using System;

namespace Chetch.Arduino.Devices.Infrared;

public class IRTransmitter : ArduinoDevice
{
    #region Fields
    #endregion

    #region Constructors
    public IRTransmitter(byte id, String sid, String? name = null) : base(id, sid, name)
    {
        //empty for now
    }
    #endregion

    #region Methods
    public void Transmit(IRData data)
    {
        SendCommand(DeviceCommand.SEND, (UInt16)data.Protocol, data.Address, data.Command);
    }

    public Task TransmitAsync(List<IRData> data, int delay)
    {
        if (delay <= 0)
        {
            throw new ArgumentException("Delay must be positive");
        }

        return Task.Run(() =>
        {
            foreach (var d in data)
            {
                Transmit(d);
                if (d != data.Last())
                {
                    Thread.Sleep(delay);
                }
            }
        });
    }
    #endregion
}
