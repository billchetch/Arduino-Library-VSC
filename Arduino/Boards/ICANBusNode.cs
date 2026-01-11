using System;
using Chetch.Arduino.Devices.Comms;
using Chetch.Arduino.Connections;
using Chetch.Messaging;

namespace Chetch.Arduino.Boards;

public interface ICANBusNode
{
    byte ID { get; }

    byte NodeID => MCPDevice.NodeID;

    MCP2515 MCPDevice { get; }

    IEnumerable<MCP2515.ErrorLogEntry> ErrorLog => MCPDevice.ErrorLog;

    MessageIO<ArduinoMessage> IO { get; set; }

    IConnection? Connection {get; set; }

    void Begin();

    Task End();
}
