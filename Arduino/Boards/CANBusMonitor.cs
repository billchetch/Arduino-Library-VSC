using System;
using Chetch.Arduino.Devices.Comms;
using Chetch.Messaging;


namespace Chetch.Arduino.Boards;

public class CANBusMonitor : ArduinoBoard
{
    #region Constants   
    public const String DEFAULT_BOARD_NAME = "canbusmon";

    public const uint REQUEST_BUS_NODES_STATUS_INTERVAL = 2000; //in ms
    #endregion

    #region Properties
    public MCP2515 MasterNode { get; } = new MCP2515("master");

    public Dictionary<byte, MCP2515> RemoteNodes { get; } = new Dictionary<byte, MCP2515>();
    #endregion

    #region Events
    public EventHandler<MCP2515>? RemoteNodeFound;
    #endregion

    #region Fields
    System.Timers.Timer requestBusNodesStatus = new System.Timers.Timer();
    #endregion

    #region Constructors
    public CANBusMonitor(String sid = DEFAULT_BOARD_NAME) : base(sid)
    {
        requestBusNodesStatus.AutoReset = true;
        requestBusNodesStatus.Interval = REQUEST_BUS_NODES_STATUS_INTERVAL;
        requestBusNodesStatus.Elapsed += (sender, eargs) =>
        {
            if (MasterNode.CanSend)
            {
                MasterNode.RequestStatus();
                MasterNode.RequestNodesStatus();
            }
        };

        MasterNode.BusMessageReceived += (sender, eargs) =>
        {
            byte nodeID = eargs.CanID.NodeID;
            bool isRemoteNode = nodeID != MasterNode.NodeID;
            if (isRemoteNode)
            {
                if (!RemoteNodes.ContainsKey(nodeID))
                {
                    var remoteNode = new MCP2515("rn" + nodeID);
                    remoteNode.StatusFlagsChanged += handleStatusFlagsChanged;
                    remoteNode.ErrorFlagsChanged += handleErrorFlagsChanged;

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
        };

        MasterNode.StatusFlagsChanged += handleStatusFlagsChanged;
        MasterNode.ErrorFlagsChanged += handleErrorFlagsChanged;

        MasterNode.ReadyToSend += (sender, ready) =>
        {
            if (ready)
            {
                requestBusNodesStatus.Start();
                MasterNode.RequestNodesStatus();
            }
        };

        AddDevice(MasterNode);
    }
    #endregion

    #region Messaging

    void handleStatusFlagsChanged(Object? sender, MCP2515.FlagsChangedEventArgs eargs)
    {
        if (sender == null) return;

        MCP2515 mcp = (MCP2515)sender;
        Console.WriteLine(" {0} status flags changed: {1} - {2}", mcp.NodeID, Utilities.Convert.ToBitString(eargs.Flags), Utilities.Convert.ToBitString(eargs.FlagsChanged));
    }
    
    void handleErrorFlagsChanged(Object? sender, MCP2515.FlagsChangedEventArgs eargs)
    {
        if (sender == null) return;
        
        MCP2515 mcp = (MCP2515)sender;
        Console.WriteLine(" {0} error flags changed: {1} - {2}", mcp.NodeID, Utilities.Convert.ToBitString(eargs.Flags), Utilities.Convert.ToBitString(eargs.FlagsChanged));
    }
    #endregion
}
