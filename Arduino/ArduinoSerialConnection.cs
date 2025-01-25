using System;
using System.IO.Ports;
using Chetch.Utilities;

namespace Chetch.Arduino;

public class ArduinoSerialConnection : SerialPortConnection, IConnection
{

    #region Fields
    int productID = -1;
    String? productName = null;

    #endregion

    #region Constructors
    public ArduinoSerialConnection(int productID, int baudRate, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One) 
        : base(baudRate, parity, dataBits, stopBits)
    {
        this.productID = productID;
    }

    public ArduinoSerialConnection(String productName, int baudRate, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One) 
        : base(baudRate, parity, dataBits, stopBits)
    {
        this.productName = productName;
    }
    #endregion

    protected override string getPortName()
    {
        String searchFor = String.Empty;
        String searchKey = String.Empty;
        if(OperatingSystem.IsMacOS())
        {
            //product ID takes priority
            if(productID > 0)
            {
                searchFor = productID.ToString();
                searchKey = "idProduct";
            }
            else if(!String.IsNullOrEmpty(productName))
            {
                //searchFor = "usb-u-blox"; //Full name: usb-u-blox_AG_-_www.u-blox.com_u-blox_7_-_GPS_GNSS_Receiver-if00 
                //searchFor = "usb-Arduino"; //Full name: usb-Arduino__www.arduino.cc__0043_55936343034351B0A061-if00
                searchFor = "usb-1a86_USB_Serial"; //Full name (generic arduino nano): usb-1a86_USB_Serial-if00-port0
            }
            else
            {
                throw new Exception("No valid data to search on for finding port name");
            }
        }
        else if(OperatingSystem.IsLinux())
        {
            searchFor = "";  //TODO: complete
        }
        else
        {
            throw new Exception(String.Format("Operation system {0} is not supported", Environment.OSVersion.Platform));
        }
        return GetPortNameForDevice(searchFor, searchKey);
    }
}
