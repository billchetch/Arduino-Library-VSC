using System;
using Chetch.Arduino.Devices.Comms;
using Chetch.Arduino.Connections;
using Chetch.Messaging;


namespace Chetch.Arduino.Boards;

public enum CANNodeState
{
    NOT_SET, //Before the canbus monitor
    SILENT, //If we have heard nothing for some period of time
    TRANSMITTING_ONLY, //If we are receiving messages but no responses
    RESPONDING //if we are receiving reponses (this is the desired state)
}

public interface ICANBusNode
{
    byte ID { get; }

    bool IsReady { get; }

    byte NodeID => MCPDevice.NodeID;

    CANNodeState NodeState {get; }

    MCP2515 MCPDevice { get; }

    IEnumerable<MCP2515.ErrorLogEntry> ErrorLog => MCPDevice.ErrorLog;

    MessageIO<ArduinoMessage> IO { get; set; }

    public bool RouteMessage(ArduinoMessage message);

    IConnection? Connection {get; set; }

    void Begin();

    Task End();
}
