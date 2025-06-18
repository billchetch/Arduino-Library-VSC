using System;
using System.Text.Json.Serialization;
using XmppDotNet.Xmpp.ExtendedStanzaAddressing;

namespace Chetch.Arduino.Devices.Infrared;

public class IRData
{
    public enum IRProtocol
    {
        SAMSUNG = 20,
    }

    [JsonPropertyName("protocol")]
    public IRProtocol Protocol { get; set; }

    [JsonPropertyName("address")]
    public UInt16 Address { get; set; }

    [JsonPropertyName("command")]
    public UInt16 Command { get; set; }

    [JsonPropertyName("command_alias")]
    public String CommandAlias { get; set; } = String.Empty;

    public IRData(IRProtocol protocol, UInt16 address, UInt16 command)
    {
        Protocol = protocol;
        Address = address;
        Command = command;
    }

    override public String ToString()
    {
        String s = String.Format("({0},{1},{2})", Protocol, Address, Command);
        if (!String.IsNullOrEmpty(CommandAlias))
        {
            s = CommandAlias + " " + s;
        }
        return s;
    }
}
