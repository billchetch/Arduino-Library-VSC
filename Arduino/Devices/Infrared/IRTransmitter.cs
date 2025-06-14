using System;

namespace Chetch.Arduino.Devices.Infrared;

public class IRTransmitter : ArduinoDevice
{
    #region Fields
    public Dictionary<String, List<IRData>> sequences = new Dictionary<String, List<IRData>>();
    #endregion

    #region Constructors
    public IRTransmitter(byte id, String sid, String? name = null) : base(id, sid, name)
    {
        //empty for now
    }
    #endregion

    #region Methods
    public List<IRData> CreateSequence(String name)
    {
        if (!sequences.ContainsKey(name))
        {
            sequences[name] = new List<IRData>();
        }
        return sequences[name];
    }

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
                Thread.Sleep(delay);
            }
        });
    }

    public async void TransmitSequence(String name, int delay)
    {
        if (!sequences.ContainsKey(name))
        {
            throw new ArgumentException(String.Format("Sequence {0} not found", name));
        }

        await TransmitAsync(sequences[name], delay);
    }
    #endregion
}
