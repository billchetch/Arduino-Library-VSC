using System;
using Chetch.Arduino.Devices.Comms.CAN;
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

public class CANMessage{

    public enum Format{
        NONE,
        ID_AS_BITS,
        CAN_DATA_AS_BYTE_ARRAY,
    }

    public byte NodeID { get; internal set; }
    public UInt32 ID { get; internal set; }

    public byte[] Data;

    public ArduinoMessage Message { get; internal set; } = new ArduinoMessage();

    public CANMessage(byte nodeID, UInt32 canID, byte[] canData){
        NodeID = nodeID;
        ID = canID;
        Data = canData;
    }

    public String? ToString(Format format){
        switch(format){
            case Format.ID_AS_BITS:
                return Utilities.Convert.ToBitString(ID);

            case Format.CAN_DATA_AS_BYTE_ARRAY:
                return "n/a"; //Utilities.Convert.ToByteString(Data);

            case Format.NONE:
            default:
                return this.ToString();
        }
    }
}

public interface ICANBusNode : IArduinoBoard
{
    ICANDevice CANDevice { get; }

    byte NodeID => CANDevice.NodeID;

    CANNodeState NodeState { get; }

    bool RouteBusMessage(CANMessage busMessage);
}
