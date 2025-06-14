using System;
using XmppDotNet.Xmpp.ExtendedStanzaAddressing;

namespace Chetch.Arduino.Devices.Infrared;

public class IRData
{
    public enum IRProtocol
    {
        SAMSUNG = 20,
    }

    public IRProtocol Protocol { get; set; }
    public UInt16 Address { get; set; }

    public UInt16 Command { get; set; }

    public IRData(IRProtocol protocol, UInt16 address, UInt16 command)
    {
        Protocol = protocol;
        Address = address;
        Command = command;
    }
}
