using System;
using System.Threading.Tasks;
using Chetch.Bluetooth;

namespace Chetch.Arduino.Connections;

public class ArduinoBLEConnection : IConnection
{
    public bool IsConnected => cbm.IsConnected && cbm.IsReady;

    public event EventHandler<byte[]>? DataReceived;
    public event EventHandler<bool>? Connected;

    #region Fields
    CoreBluetoothManager cbm;
    CoreBluetoothManager.PeripheralDevice peripheralDevice = CoreBluetoothManager.PeripheralDevice.NOT_SPECIFIED;
    #endregion

    #region Constructors
    public ArduinoBLEConnection(CoreBluetoothManager.PeripheralDevice device)
    {
        cbm = CoreBluetoothManager.Instance;
        peripheralDevice = device;

        cbm.Ready += (sender, ready) =>
        {
            Connected?.Invoke(this, ready);    
        };

        cbm.DataReceived += (sender, data) =>
        {
            DataReceived?.Invoke(this, data);
        };
    }
    #endregion

    public void Connect()
    {
        cbm.ScanForPeripheral(peripheralDevice);
    }

    public void Disconnect()
    {
        cbm.DisconnectPeripheral();
    }


    public void Reconnect()
    {
        if (cbm.IsConnected)
        {
            Disconnect();
            while (IsConnected)
            {
                Thread.Sleep(500);
                Console.WriteLine("Waiting to disconnect...");
            }
        }
        Connect();
    }

    public void SendData(byte[] data)
    {
        cbm.WriteToPeripheral(data);
    }
}
