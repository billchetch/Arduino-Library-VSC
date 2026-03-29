using System;
using System.IO.Ports;
using Chetch.Utilities;

namespace Chetch.Arduino.Connections;

public class ArduinoSerialConnection : SerialPortConnection, IConnection
{
    
    static readonly int[] validProductIDs = [
        0x7523,
        0x0043,
        0xFFFF, //This is a dummy product ID
    ];

    #region Fields
    String devicePath;

    #endregion

    #region Constructors
    public ArduinoSerialConnection(String devicePath, int baudRate, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One) 
        : base(baudRate, parity, dataBits, stopBits)
    {
        this.devicePath = devicePath;
    }


    #endregion

    protected override String GetPortName()
    {
        var devices = SerialPortConnection.GetDevices(devicePath);
        foreach (var f in devices)
        {
            SerialPortConnection.DeviceInfo? di = null;
            try
            {
                 di = SerialPortConnection.GetUSBDeviceInfo(f);    
            } catch {}
            if(di == null)
            {
                di = SerialPortConnection.GetBluetoothDeviceInfo(f);
            }
            if(di != null)
            {
                if (validProductIDs.Contains(di.ProductID))
                {
                    return di.PortName;
                }
                throw new Exception(String.Format("{0} is not a valid product ID", di.ProductID));
            }
        }
        throw new Exception(String.Format("Failed to find port for device path {0}", devicePath));
    }
}
