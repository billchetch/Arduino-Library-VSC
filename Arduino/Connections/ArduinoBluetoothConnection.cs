using System;
using System.Threading.Tasks;
using Chetch.Bluetooth;
using Chetch.Utilities;

namespace Chetch.Arduino.Connections;

/// <summary>
/// </summary>

public class ArduinoBluetoothConnection : BluetoothSerialConnection, IConnection
{
    #region Fields
    IBluetoothManager? bm;
    IBluetoothManager.BluetoothDevice device;
    #endregion

    #region Constructors
    public ArduinoBluetoothConnection(IBluetoothManager.BluetoothDevice device) : base(String.Empty, 9600)
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
    }
    #endregion

    public void Connect()
    {
        if (OperatingSystem.IsMacOS())
        {
            var di = bm.Search(device);
            SetDevicePath(di.Path);
        }
        
        base.Connect();
    }
}
