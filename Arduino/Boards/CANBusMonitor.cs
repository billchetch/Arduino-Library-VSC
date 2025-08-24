using System;
using Chetch.Arduino.Devices.Comms;
using Chetch.Messaging;


namespace Chetch.Arduino.Boards;

public class CANBusMonitor : ArduinoBoard
{
    #region Constants   
    public const String DEFAULT_BOARD_NAME = "canbusmon";
    #endregion

    #region Properties
    public MCP2515 MasterNode { get; } = new MCP2515("master");

    public Dictionary<byte, MCP2515> RemoteNodes { get; } = new Dictionary<byte, MCP2515>();
    #endregion

    #region Events
    public EventHandler<MCP2515>? RemoteNodeFound;
    #endregion

    #region Constructors
    public CANBusMonitor(String sid = DEFAULT_BOARD_NAME) : base(sid)
    {
        MasterNode.MessageForwarded += (sender, eargs) =>
        {
            byte nodeID = eargs.CanID.NodeID;
            bool isRemoteNode = nodeID != MasterNode.NodeID;
            if (isRemoteNode)
            {
                if (!RemoteNodes.ContainsKey(nodeID))
                {
                    var remoteNode = new MCP2515("rn" + nodeID);
                    remoteNode.StatusFlagsChanged += (sender, eargs) =>
                    {

                    };

                    remoteNode.ErrorFlagsChanged += (sender, eargs) =>
                    {
                        
                    };

                    //Add the remote node
                    RemoteNodes[nodeID] = remoteNode;
                    RemoteNodeFound?.Invoke(this, remoteNode);
                }

                switch (eargs.Message.Type)
                {
                    case MessageType.STATUS_RESPONSE:
                        ArduinoMessageMap.AssignMessageValues(RemoteNodes[nodeID], eargs.Message);
                        break;
                }
            }


            //eargs.
            //Console.WriteLine("Forwarded message of priority {0} and type{1} from Node {2} and device {3} with structure {4}", eargs.CanID.Priority, eargs.CanID.Messagetype, eargs.CanID.NodeID, eargs.CanID.Sender, eargs.CanID.MessageStructure);
        };

        MasterNode.Ready += (sender, ready) =>
        {
            if (ready)
            {
                MasterNode.RequestNodesStatus();
            }
        };

        AddDevice(MasterNode);
    }
    #endregion
}
