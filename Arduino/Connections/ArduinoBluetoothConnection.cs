using System;
using System.Threading.Tasks;
using Chetch.Bluetooth;
using Chetch.Utilities;

namespace Chetch.Arduino.Connections;

/// <summary>
/// </summary>

public class ArduinoBluetoothConnection : IConnection
{
    #region Properties
    public bool IsConnected => throw new NotImplementedException();

    #endregion

    #region Events
    public event EventHandler<byte[]> DataReceived;
    public event EventHandler<bool> Connected;
    #endregion

    #region Fields
    IBluetoothManager? bm;
    IBluetoothManager.BluetoothDevice device;
    #endregion

    #region Constructors
    public ArduinoBluetoothConnection(IBluetoothManager.BluetoothDevice device)
    {
        switch (device)
        {
            case IBluetoothManager.BluetoothDevice.JDY_31:
                if (OperatingSystem.IsMacOS())
                {
                    bm = BlueUtilManager.Instance;
                }
                this.device = device;
                break;

            default:
                throw new NotImplementedException();
        }

        bm.Ready += (sender, ready) =>
        {
            Connected?.Invoke(this, ready);
        };

        bm.DataReceived += (sender, data) =>
        {
            DataReceived?.Invoke(this, data);  
        };
    }
    #endregion

    public void Connect()
    {
       bm.Connect(device);
    }

    public void Disconnect()
    {
        bm.Disconnect();
    }

    public void Reconnect()
    {
        bm.Disconnect();
        Thread.Sleep(500);
        bm.Connect(device);
    }
    
    public void SendData(byte[] data)
    {
        bm.SendData(data);
    }
}
