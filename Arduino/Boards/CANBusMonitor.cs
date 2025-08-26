using System;
using System.Collections.Immutable;
using Chetch.Arduino.Devices.Comms;
using Chetch.Messaging;
using XmppDotNet.Xmpp.Delay;


namespace Chetch.Arduino.Boards;

public class CANBusMonitor : ArduinoBoard
{
    #region Constants   
    public const String DEFAULT_BOARD_NAME = "canbusmon";

    public const uint REQUEST_BUS_NODES_STATUS_INTERVAL = 10000; //in ms
    #endregion

    #region Properties
    public MCP2515 MasterNode { get; } = new MCP2515("master");

    public Dictionary<byte, MCP2515> RemoteNodes { get; } = new Dictionary<byte, MCP2515>();

    public int BusSize { get; internal set; } = 0; //expected number of nodes
    #endregion

    #region Events
    public EventHandler<MCP2515>? RemoteNodeFound;
    public EventHandler<List<MCP2515>>? AllNodesFound;
    #endregion

    #region Fields
    System.Timers.Timer requestBusNodesStatus = new System.Timers.Timer();
    #endregion

    #region Constructors
    public CANBusMonitor(int busSize, String sid = DEFAULT_BOARD_NAME) : base(sid)
    {
        requestBusNodesStatus.AutoReset = true;
        requestBusNodesStatus.Interval = REQUEST_BUS_NODES_STATUS_INTERVAL;
        requestBusNodesStatus.Elapsed += (sender, eargs) =>
        {
            if (MasterNode.CanSend)
            {
                MasterNode.RequestStatus();
                MasterNode.RequestRemoteNodesStatus();
            }
        };

        MasterNode.BusMessageReceived += (sender, eargs) =>
        {
            byte nodeID = eargs.CanID.NodeID;
            bool isRemoteNode = nodeID != MasterNode.NodeID;
            if (isRemoteNode)
            {
                try
                {
                    bool newNode = !RemoteNodes.ContainsKey(nodeID);
                    if (newNode)
                    {
                        if (BusSize > 0 && RemoteNodes.Count == BusSize - 1)
                        {
                            throw new Exception(String.Format("Cannot add remote node as bus size is set to {0}", BusSize));
                        }
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
                            ArduinoMessage msg = new ArduinoMessage(eargs.Message.Type);
                            msg.Add(0); //report interval (base class property)
                            msg.Add(nodeID); //property with index 1
                            foreach (var arg in eargs.Message.Arguments)
                            {
                                if (arg != null)
                                {
                                    msg.Add(arg);
                                }
                            }
                            msg.Add(true); //CanSend property which is surely true if this message has been received
                            ArduinoMessageMap.AssignMessageValues(RemoteNodes[nodeID], msg);
                            break;
                    }

                    if (newNode && RemoteNodes.Count == busSize - 1)
                    {
                        AllNodesFound?.Invoke(this, RemoteNodes.Values.ToList());
                        MasterNode.SynchroniseBus();
                        Console.WriteLine("All nodes found");
                    }
                }
                catch (Exception)
                {
                    
                }
            }
        };

        MasterNode.StatusFlagsChanged += handleStatusFlagsChanged;
        MasterNode.ErrorFlagsChanged += handleErrorFlagsChanged;

        MasterNode.ReadyToSend += (sender, ready) =>
        {
            if (ready)
            {
                Console.WriteLine("Master Node is ready to send!");
                //requestBusNodesStatus.Start();
                //MasterNode.RequestRemoteNodesStatus();
                MasterNode.SynchroniseBus();
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
        Console.WriteLine("Node {0} status flags changed: {1} - {2}", mcp.NodeID, Utilities.Convert.ToBitString(eargs.Flags), Utilities.Convert.ToBitString(eargs.FlagsChanged));
    }
    
    void handleErrorFlagsChanged(Object? sender, MCP2515.FlagsChangedEventArgs eargs)
    {
        if (sender == null) return;
        
        MCP2515 mcp = (MCP2515)sender;
        Console.WriteLine("Node {0} error flags changed: {1} - {2}", mcp.NodeID, Utilities.Convert.ToBitString(eargs.Flags), Utilities.Convert.ToBitString(eargs.FlagsChanged));
    }
    #endregion
}
