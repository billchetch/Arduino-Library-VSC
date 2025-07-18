using System;
using System.Net.NetworkInformation;
using Chetch.Utilities;

namespace Chetch.Arduino.Connections;

public class ArduinoLocalSocketConnection : LocalSocketConnection, IConnection, IConnectionListener
{
    static public String SocketPathForBoard(String boardName)
    {
        return String.Format("/tmp/unix_socket_{0}", boardName);
    }

    static public String SocketPathForBoard(ArduinoBoard board)
    {
        return String.Format("/tmp/unix_socket_{0}", board.SID);
    }

    public ArduinoLocalSocketConnection(string path) : base(path)
    {
    }


}
