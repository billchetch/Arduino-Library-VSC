using System;

namespace Chetch.Arduino.Devices.Comms.Serial;

public class SerialPinMaster : SerialPin
{
    #region Constants
    public const String SPIN_MASTER_SID = "spinm";
    #endregion

    public SerialPinMaster(string sid = SPIN_MASTER_SID, string? name = null) : base(sid, name)
    {
    }

    public void Send(byte[] data)
    {
        if(data.Length != BufferSize)
        {
            throw new Exception("Data and buffer are not equal");
        }
        SendCommand(DeviceCommand.SEND, data);
    }

    public void Send(DeviceCommand command, params Object[] arguments)
    {
        List<byte> data = new List<byte>();
        data.Add((byte)command);

        foreach(var arg in arguments)
        {
            var bytes = Chetch.Utilities.Convert.ToBytes(arg);
            if(data.Count + bytes.Length > BufferSize)
            {
                break;
            } 
            else
            {
                data.AddRange(bytes);
            }
        }

        //finally we pad with 0s
        for(int i = data.Count; i < BufferSize; i++)
        {
            data.Add(0);
        }

        Send(data.ToArray());
    }
}
