using System;
using System.IO.Ports;
using Chetch.Utilities;

namespace Chetch.Arduino;

public class ArduinoSerialConnection : SerialPortConnection, IConnection
{

    #region Fields
    String[] searches;

    #endregion

    #region Constructors
    public ArduinoSerialConnection(String searches, int baudRate, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One) 
        : base(baudRate, parity, dataBits, stopBits)
    {
        this.searches = searches.Split(',');
    }

    
    #endregion

    protected override String getPortName()
    {
        String searchKey = String.Empty;
        
        if(OperatingSystem.IsMacOS())
        {
            //product ID takes priority
            searchKey = "idProduct";
        }
        else if(OperatingSystem.IsLinux())
        {
            //Currently do nothing (ex: searchFor = "usb-1a86_USB";)
        }
        else
        {
            throw new Exception(String.Format("Operation system {0} is not supported", Environment.OSVersion.Platform));
        }

        List<Exception> exceptions = [];
        foreach(var searchFor in searches)
        {
            try
            {
                return GetPortNameForDevice(searchFor, searchKey);
            }
            catch(Exception e)
            {
                //do nothing
                exceptions.Add(e);
            }
        }

        //by here we have failed
        throw new AggregateException(exceptions);
    }
}
