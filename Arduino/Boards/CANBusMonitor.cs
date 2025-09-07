using System;
using System.Collections.Immutable;
using System.Reflection;
using Chetch.Arduino.Devices.Comms;
using Chetch.Messaging;
using XmppDotNet.Xmpp.Delay;


namespace Chetch.Arduino.Boards;

public class CANBusMonitor : CANBusNode
{
    #region Constants   
    public const String DEFAULT_BOARD_NAME = "canbusmon";

    public const uint REQUEST_BUS_NODES_STATUS_INTERVAL = 10000; //in ms
    #endregion

    #region Classes and Enums

    #endregion

    #region Properties
    public MCP2515 MasterNode => MCPNode;

    public bool AllNodesReady => nodeReadyCount == BusSize;

    public int BusSize => 1 + RemoteNodes.Count;

    public Dictionary<byte, CANBusNode> RemoteNodes { get; } = new Dictionary<byte, CANBusNode>();
    #endregion

    #region Events
    public EventHandler<CANBusNode>? NodeReady;

    public EventHandler<bool>? NodesReady;

    public EventHandler<MCP2515.BusMessageEventArgs>? BusMessageReceived;

    public EventHandler? RequestedBusStatus;
    #endregion

    #region Fields
    System.Timers.Timer requestBusNodesStatus = new System.Timers.Timer();
    
    int nodeReadyCount = 0;
    #endregion

    #region Constructors
    public CANBusMonitor(String sid = DEFAULT_BOARD_NAME) : base(MASTER_NODE_ID, sid)
    {
        requestBusNodesStatus.AutoReset = true;
        requestBusNodesStatus.Interval = REQUEST_BUS_NODES_STATUS_INTERVAL;
        requestBusNodesStatus.Elapsed += (sender, eargs) =>
        {
            if (MasterNode.CanSend)
            {
                MasterNode.RequestStatus();
                MasterNode.RequestRemoteNodesStatus();
                RequestedBusStatus?.Invoke(this, EventArgs.Empty);
            }
        };

        //The master node on the Arduino Board has parceled up a bus message and sent it as a normal arduino message
        //to this board which in turn directs it to the MCP node that then unwraps the message and fires this event.  The main purpose here
        //is to take tthe unwrapped message and pass it to the appropriate remote node.
        MasterNode.BusMessageReceived += (sender, eargs) =>
        {
            if (eargs.NodeID == NodeID)
            {
                HandleBusMessage(eargs.CanID, eargs.CanData.ToArray(), eargs.Message);
            }
            else
            {
                if (!RemoteNodes.ContainsKey(eargs.NodeID))
                {
                    throw new Exception(String.Format("Unrecognised node: {0}", eargs.NodeID));
                }
                RemoteNodes[eargs.NodeID].HandleBusMessage(eargs.CanID, eargs.CanData.ToArray(), eargs.Message);
            }

            BusMessageReceived?.Invoke(this, eargs);
        };
        MasterNode.Ready += handleNodeReady;

        MasterNode.ReadyToSend += (sender, ready) =>
        {
            if (ready)
            {
                requestBusNodesStatus.Start();
                MasterNode.RequestRemoteNodesStatus();
            }
        };
    }

    public CANBusMonitor(String sid, int remoteNodes) : this(sid)
    {
        for (int i = 0; i < remoteNodes; i++)
        {
            AddRemoteNode();
        }
    }
    #endregion

    #region Methods
    void handleNodeReady(Object? sender, bool ready)
    {
        if (sender == null) return;
        MCP2515 mcp = (MCP2515)sender;
        if (mcp.Board == null) return;
        CANBusNode node = (CANBusNode)mcp.Board;
        
        if (mcp == MCPNode || RemoteNodes.Values.Contains(node))
        {
            bool previouslyReady = AllNodesReady;
            nodeReadyCount += ready ? 1 : -1;

            NodeReady?.Invoke(this, node);
            if (AllNodesReady) //was not ready, now it is
            {
                //all nodes ready so send a synchrnoise command in case it's used by the nodes (do not expect a response)
                MasterNode.SynchroniseBus();

                //Fire the event
                NodesReady?.Invoke(this, true);
            }
            else if (previouslyReady) //was ready now is not
            {
                NodesReady?.Invoke(this, false);
            }
        }
    }

    public void AddRemoteNode(CANBusNode remoteNode)
    {
        if (remoteNode.NodeID == 0)
        {
            remoteNode.SetNodeID((byte)(MASTER_NODE_ID + RemoteNodes.Count + 1));
        }

        if (RemoteNodes.ContainsKey(remoteNode.NodeID))
        {
            throw new ArgumentException(String.Format("Node {0} already added!", remoteNode.ID));
        }
        RemoteNodes[remoteNode.NodeID] = remoteNode;
        remoteNode.MCPNode.Ready += handleNodeReady;
    }

    public void AddRemoteNode()
    {
        AddRemoteNode(new CANBusNode());
    }

    #endregion

    #region Lifecycle
    public override void End()
    {
        requestBusNodesStatus.Stop();

        base.End();
    }
    #endregion

    #region Messaging
    
    #endregion
}
