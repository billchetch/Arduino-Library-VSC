using System;
using System.IO.Ports;
using Chetch.Utilities;

namespace Chetch.Arduino.Connections;

public class ArduinoSerialConnection : SerialPortConnection, IConnection
{
    
    static readonly int[] validProductIDs = [
        0x7523,
        0x0043
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
        var devices = SerialPortConnection.GetUSBDevices(devicePath);
        foreach (var f in devices)
        {
            SerialPortConnection.USBDeviceInfo di = SerialPortConnection.GetUSBDeviceInfo(f);
            if (validProductIDs.Contains(di.ProductID))
            {
                return di.PortName;
            }
        }
        throw new Exception(String.Format("Failed to find port for device path {0}", devicePath));
    }
}
