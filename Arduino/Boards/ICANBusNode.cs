using System;
using Chetch.Arduino.Devices.Comms;
using Chetch.Arduino.Connections;
using Chetch.Messaging;
using XmppDotNet.Xmpp.MessageEvents;


namespace Chetch.Arduino.Boards;

public enum CANNodeState
{
    NOT_SET, //Before the canbus monitor
    SILENT, //If we have heard nothing for some period of time
    TRANSMITTING, //If we are receiving messages but no responses
    RESPONDING //if we are receiving reponses (this is the desired state)
}

public class CANNodeStateChange : System.EventArgs
{
    public byte NodeID {get; }

    public CANNodeState NewState { get; }

    public CANNodeState OldState { get; }

    public CANNodeStateChange(byte nodeID, CANNodeState newValue, CANNodeState oldValue)
    {
        NodeID = nodeID;
        NewState = newValue;
        OldState = oldValue;
    }

    public override string ToString()
    {
        return String.Format("N{0} changed from {1} to {2}", NodeID, OldState, NewState);
    }
}

public interface ICANBusNode
{
    byte ID { get; }

    bool IsReady { get; }

    byte NodeID { get; }

    CANNodeState NodeState {get; }

    EventHandler<CANNodeStateChange>? NodeStateChanged { get; set; }

    MCP2515 MCPDevice { get; }

    IEnumerable<MCP2515.ErrorLogEntry> ErrorLog => MCPDevice.ErrorLog;

    MessageIO<ArduinoMessage> IO { get; set; }

    public bool RouteMessage(ArduinoMessage message);

    IConnection? Connection {get; set; }

    void Begin();

    Task End();
}
