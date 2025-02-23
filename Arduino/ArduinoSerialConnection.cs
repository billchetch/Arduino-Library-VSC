using System;
using System.IO.Ports;
using Chetch.Utilities;

namespace Chetch.Arduino;

public class ArduinoSerialConnection : SerialPortConnection, IConnection
{

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
        
        throw new Exception(String.Format("Failed to find port for device path {0}", devicePath));
    }
}
