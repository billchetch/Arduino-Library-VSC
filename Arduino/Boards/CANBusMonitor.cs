using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Chetch.Arduino.Devices.Comms;
using Chetch.Messaging;
using XmppDotNet.Xmpp.Delay;


namespace Chetch.Arduino.Boards;

public class CANBusMonitor : CANBusNode
{
    #region Constants   
    public const String DEFAULT_BOARD_NAME = "canbusmon";

    public const uint REQUEST_BUS_NODES_STATUS_INTERVAL = 5000; //in ms
    #endregion

    #region Classes and Enums
    public class BusNodeActivity
    {
        #region Properties
        public uint MessageCount { get; internal set; } = 0;
        public double MessageRate { get; internal set; } = -1.0;

        public UInt32 Latency { get; internal set; } = 0;
        public UInt32 MaxLatency {get; internal set; } = 0;

        public DateTime LastIdleOn { get; internal set; }

        public bool IsIdle => MessageRate == 0;

        public TimeSpan IdleFor => IsIdle ? DateTime.Now - LastIdleOn : default(TimeSpan);
        #endregion

        #region Fields
        private uint lastMessageCount = 0;
        #endregion

        #region Methods
        public void UpdateMessageCount(UInt32 estimatedNodeMillis, byte messageTimestamp, int timestampResolution)
        {
            if(timestampResolution >= 0)
            {
                int estimatedTimestamp = (int)((estimatedNodeMillis >> timestampResolution) & 0xFF);
                int diff = Math.Abs((int)messageTimestamp - estimatedTimestamp);
                uint diffInMillis = (uint)Math.Min(256 - diff, diff) << timestampResolution;
                Latency = diffInMillis;
                if(diffInMillis > MaxLatency)
                {
                    MaxLatency = diffInMillis;
                }
            }
            MessageCount++;
        }

        public void UpdateMessageRate(TimeSpan timeSpan)
        {
            bool oldIdle = IsIdle;
            MessageRate = (double)(MessageCount - lastMessageCount) / timeSpan.TotalSeconds;
            lastMessageCount = MessageCount;
            
            bool changed = oldIdle != IsIdle;
            if(changed && IsIdle)
            {
                LastIdleOn = DateTime.Now;
            }
        }
        #endregion

    }
    #endregion

    #region Properties
    public MCP2515Master MasterNode => (MCP2515Master)MCPNode;

    public bool AllNodesReady => nodeReadyCount == BusSize;

    public int BusSize => 1 + RemoteNodes.Count;

    public Dictionary<byte, CANBusNode> RemoteNodes { get; } = new Dictionary<byte, CANBusNode>();
    public Dictionary<byte, BusNodeActivity> BusActivity { get; } = new Dictionary<byte, BusNodeActivity>();

    public UInt32 BusMessageCount {get; internal set; } = 0;

    public double BusMessageRate {get; internal set; } = 0.0;

    public String BusSummary
    {
        get
        {
            var s = new StringBuilder();
            if (IsReady)
            {
                if (AllNodesReady)
                {
                    s.AppendFormat("Bus monitor {0}, all {1} nodes are ready!", SID, BusSize);
                }
                else
                {
                    s.AppendFormat("Bus monitor {0}, {1} nodes out of {2} are ready!", SID, nodeReadyCount, BusSize);
                }
                s.AppendFormat(" Messages={0}, Rate={1} mps", BusMessageCount, BusMessageRate);
            }
            else
            {
                s.AppendFormat("Bus monitor {0} is not ready", SID);
            }

            return s.ToString();
        }
    }
    #endregion

    #region Events
    public EventHandler<CANBusNode>? NodeReady;

    public EventHandler<bool>? NodesReady;

    public EventHandler<MCP2515Master.BusMessageEventArgs>? BusMessageReceived;

    public EventHandler? RequestedBusStatus;
    #endregion

    #region Fields
    System.Timers.Timer requestBusNodesStatus = new System.Timers.Timer();
    
    int nodeReadyCount = 0;
    #endregion

    #region Constructors
    public CANBusMonitor(String sid = DEFAULT_BOARD_NAME) : base(new MCP2515Master(CANBusNode.MASTER_NODE_ID), sid)
    {
        requestBusNodesStatus.AutoReset = true;
        requestBusNodesStatus.Interval = REQUEST_BUS_NODES_STATUS_INTERVAL;
        requestBusNodesStatus.Elapsed += (sender, eargs) =>
        {
            RequestNodesStatus();
            RequestedBusStatus?.Invoke(this, EventArgs.Empty);
            
            BusMessageRate = 0.0;
            foreach(var ba in BusActivity.Values)
            {
                ba.UpdateMessageRate(TimeSpan.FromMilliseconds(requestBusNodesStatus.Interval));
                BusMessageRate += ba.MessageRate;
            }
        };

        //The master node on the Arduino Board has parceled up a bus message and sent it as a normal arduino message
        //to this board which in turn directs it to the MCP node that then unwraps the message and fires this event.  The main purpose here
        //is to take tthe unwrapped message and pass it to the appropriate remote node.
        MasterNode.BusMessageReceived += (sender, eargs) =>
        {
            CANBusNode busNode;
            if (eargs.NodeID == NodeID)
            {
                busNode = this;
            }
            else
            {
                if (!RemoteNodes.ContainsKey(eargs.NodeID))
                {
                    throw new Exception(String.Format("Unrecognised node: {0}", eargs.NodeID));
                }
                busNode = RemoteNodes[eargs.NodeID];
            }
            if (!BusActivity.ContainsKey(busNode.NodeID))
            {
                BusActivity.Add(busNode.NodeID, new BusNodeActivity());
            }
            
            BusActivity[busNode.NodeID].UpdateMessageCount(busNode.MCPNode.EstimatedNodeMillis,
                                                            eargs.CanID.Timestamp,
                                                            busNode.MCPNode.TimestampResolution);

            BusMessageCount++;
            busNode.HandleBusMessage(eargs.CanID, eargs.CanData.ToArray(), eargs.Message);
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

    public CANBusMonitor(int remoteNodes, String sid = DEFAULT_BOARD_NAME) : this(sid)
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
                //all nodes ready so send an INITIALISE message
                InitialiseNodes();

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
        byte nid = (byte)(MASTER_NODE_ID + RemoteNodes.Count + 1);
        AddRemoteNode(new CANBusNode(nid));
    }

    public List<CANBusNode> GetAllNodes()
    {
        var l = new List<CANBusNode> { this };
        l.AddRange(RemoteNodes.Values);
        return l;
    }
    
    public CANBusNode GetNode(byte nodeID)
    {
        if (nodeID == NodeID)
        {
            return this;
        }
        else if(RemoteNodes.ContainsKey(nodeID))
        {
            return RemoteNodes[nodeID];
        } else
        {
            throw new ArgumentException(String.Format("There is no node with ID {0}", nodeID));
        }
    }
    #endregion

    #region Lifecycle
    public override async Task End()
    {
        requestBusNodesStatus.Stop();
        
        FinaliseNodes();

        await base.End();
    }
    #endregion

    #region Messaging

    public void SendCommand(ArduinoDevice.DeviceCommand command, params Object[] arguments)
    {
        if (AllNodesReady)
        {
            MasterNode.SendCommand(command, arguments);
        }
        else
        {
            throw new Exception(String.Format("Cannot execute command {0} bus as not all nodes are ready", command));
        }
    }
    
    public void TestBus(byte testNumber, Int16 testParam = 0)
    {
        SendCommand(ArduinoDevice.DeviceCommand.TEST, testNumber, testParam);
    }

    public void PauseBus()
    {
        SendCommand(ArduinoDevice.DeviceCommand.PAUSE);
    }

    public void ResumeBus()
    {
        SendCommand(ArduinoDevice.DeviceCommand.RESUME);
    }

    public void RequestNodesStatus()
    {
        MasterNode.RequestStatus();
        MasterNode.RequestRemoteNodesStatus();
    }

    public void InitialiseNodes()
    {
        MasterNode.Initialise();
        MasterNode.InitialiseRemoteNode(0);
    }
    
    public void InitialiseNode(byte nodeID)
    {
        if(nodeID == NodeID)
        {
            MasterNode.Initialise();
        }
        else
        {
            MasterNode.InitialiseRemoteNode(nodeID);
        }
    }

    public void PingNodes()
    {
        MasterNode.Ping();
        MasterNode.PingRemoteNode(0);
    }

    public void PingNode(byte nodeID)
    {
        if(nodeID == NodeID)
        {
            MasterNode.Ping();
        }
        else
        {
            MasterNode.PingRemoteNode(nodeID);
        }
    }

    public void ResetNodes()
    {
        MasterNode.Reset();
        MasterNode.ResetRemoteNode(0);
    }

    public void ResetNode(byte nodeID)
    {
        if(nodeID == NodeID)
        {
            MasterNode.Reset();
        }
        else
        {
            MasterNode.ResetRemoteNode(nodeID);
        }
    }

    public void RaiseError(byte nodeID, MCP2515.MCP2515ErrorCode ecode, UInt32 edata = 0)
    {
        if(nodeID == NodeID)
        {
            MasterNode.RaiseError(ecode, edata);
        }
        else
        {
            MasterNode.RaiseRemoteNodeError(nodeID, ecode, edata);
        }
    }
    
    public void FinaliseNodes()
    {
        MasterNode.Finalise();
        MasterNode.FinaliseRemoteNode(0);
    }

    public void FinaliseNode(byte nodeID)
    {
        if(nodeID == NodeID)
        {
            MasterNode.Finalise();
        }
        else
        {
            MasterNode.FinaliseRemoteNode(nodeID);
        }
    }
    
    #endregion
}
