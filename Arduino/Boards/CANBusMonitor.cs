using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Chetch.Arduino.Connections;
using Chetch.Arduino.Devices;
using Chetch.Arduino.Devices.Comms;
using Chetch.Messaging;
using Chetch.Utilities;
using Microsoft.Extensions.Logging;
using XmppDotNet;
using XmppDotNet.Xmpp.Delay;
using XmppDotNet.Xmpp.Jingle;


namespace Chetch.Arduino.Boards;

public class CANBusMonitor : ArduinoBoard, ICANBusNode
{
    #region Constants   
    public const String DEFAULT_BOARD_NAME = "canbusmon";

    #endregion

    #region Classes and Enums
    public class BusActivity
    {
        public DateTime Created { get; }
        public uint MessageCount { get; }

        public double MessageRate { get; }
        public String IOSummary { get; }

        public BusActivity(uint messageCount = 0, double messageRate = 0.0, String ioSummary = "")
        {
            MessageCount = messageCount;
            MessageRate = messageRate;
            IOSummary = ioSummary;
            Created = DateTime.Now;
        }

        public override string ToString()
        {
            return String.Format("Message Count={0}, Message Rate={1:F1}, IO={2}", MessageCount, MessageRate, IOSummary);
        }
    }
    #endregion

    #region Properties
    public MCP2515Master MasterNode { get; } = new MCP2515Master(1);

    public MCP2515 MCPDevice => MasterNode; //for interface compliance

    public CANNodeState NodeState => MasterNode.State; //for interface compliance

    public int BusSize => 1 + RemoteNodes.Count;

    public Dictionary<byte, ICANBusNode> RemoteNodes { get; } = new Dictionary<byte, ICANBusNode>();
    
    public CircularLog<BusActivity> ActivityLog { get; } = new CircularLog<BusActivity>(64);

    public BusActivity? Activity => ActivityLog.Count > 0 ? ActivityLog.First() : null;
    public String BusSummary
    {
        get
        {
            var s = new StringBuilder();
            if (IsReady)
            {
                var allNodes = GetAllNodes();
                int nodeReadyCount = 0;
                foreach(var node in allNodes)
                {
                    if(node.IsReady)nodeReadyCount++;
                }
                if(nodeReadyCount == BusSize)
                {
                    s.AppendFormat("{0}: all {1} nodes ready!", SID, BusSize);    
                } else
                {
                    s.AppendFormat("{0}: {1} of {2} nodes ready!", SID, nodeReadyCount, BusSize);    
                }
                TimeSpan uptime = DateTime.Now - MasterNode.LastReadyOn;
                s.AppendFormat(" Uptime={0}, Messages={1}, Rate={2:F1} mps, IO={3}", 
                                    uptime.ToString("g"), 
                                    Activity != null ? Activity.MessageCount : "N/A", 
                                    Activity != null ? Activity.MessageRate : "N/A",
                                    Activity != null ? Activity.IOSummary : "N/A");
            }
            else
            {
                s.AppendFormat("{0} is not ready", SID);
            }

            return s.ToString();
        }
    }
    #endregion

    #region Events
    public EventHandler<bool>? NodeReady;

    public EventHandler<MCP2515.MCP2515ErrorCode>? NodeError;
    
    public EventHandler<MCP2515Master.BusMessageEventArgs>? BusMessageReceived;

    #endregion

    #region Fields
    System.Timers.Timer monitorNodeStateTimer = new System.Timers.Timer();
    #endregion

    #region Constructors
    public CANBusMonitor(String sid = DEFAULT_BOARD_NAME) : base(sid)
    {
        //The master node on the Arduino Board has parceled up a bus message and sent it as a normal arduino message
        //to this board which in turn directs it to the MCP node that then unwraps the message and fires this event.  The main purpose here
        //is to take tthe unwrapped message and pass it to the appropriate remote node.
        MasterNode.BusMessageReceived += (sender, eargs) =>
        {
            //Determine which node this message relates to
            ICANBusNode busNode = GetNode(eargs.NodeID);
            
            //Parse the message
            var message = eargs.Message;
            var canData = eargs.CanData;
            switch (message.Type)
            {
                case MessageType.STATUS_RESPONSE:
                    if(message.Sender == busNode.ID)
                    {
                        message.Populate<byte, UInt32, byte, Int16>(canData);
                    } 
                    else if(message.Sender == busNode.MCPDevice.ID)
                    {
                        //Status Flags, Error Flags, errorCountTX, errorCountRX, errorCountFlags
                        message.Populate<byte, byte, byte, byte, UInt16>(canData);
                        message.Add(busNode.MCPDevice.ReportInterval, 0);
                        message.Add(busNode.MCPDevice.NodeID, 1);
                    } 
                    else
                    {
                        //TODO: maybe seperate types here
                        if (HasDevice(message.Sender))
                        {
                            var device = GetDevice(message.Sender);
                            var template = ArduinoMessageMap.CreateMessageFor(device, message.Type);
                            message.Populate(template, canData, 0);
                        }
                    }
                    break;

                case MessageType.INITIALISE_RESPONSE:
                    //Millis timestamp resolution and presence interval
                    message.Populate<UInt32, byte, UInt16>(canData);
                    break;

                case MessageType.COMMAND_RESPONSE:
                    message.Populate<byte>(canData);
                    break;

                case MessageType.ERROR:
                    //Error Code, Error Data, Error Code Flags, MCP Error Flags
                    message.Populate<byte, UInt32, UInt16, byte>(canData);
                    message.Add(ArduinoBoard.ErrorCode.DEVICE_ERROR, 0);
                    break;

                case MessageType.PRESENCE:
                    //Nodemillis, Interval, Initial presence, Status Flags
                    message.Populate<UInt32, UInt16, bool, byte>(canData);
                    break;

                case MessageType.DATA:
                    if (HasDevice(message.Sender))
                    {
                        var device = GetDevice(message.Sender);
                        var template = ArduinoMessageMap.CreateMessageFor(device, message.Type);
                        message.Populate(template, canData, 0);
                    }
                    break;
            }

            busNode.IO.Inject(message);
            
            //Fire received event
            BusMessageReceived?.Invoke(busNode, eargs);
        };

        MasterNode.Ready += (sender, ready) =>
        {
            NodeReady?.Invoke(this, ready);
            MasterNode.Initialise();
        };

        MasterNode.ErrorReceived += (sender, emsg) =>
        {
            NodeError?.Invoke(this, MasterNode.LastError);
        };

        AddDevice(MasterNode);

        RequestStatusTimer.Elapsed += (sender, eargs) =>
        {
            //var statusRequest = new ArduinoMessage(MessageType.STATUS_REQUEST);
            foreach(var remoteNode in RemoteNodes.Values)
            {
                remoteNode.MCPDevice.RequestStatus();
                remoteNode.MCPDevice.LastStatusRequest = DateTime.Now;
            }
            MasterNode.RequestStatus();
        };

        MasterNode.BusActivityUpdated += (sender, eargs) =>
        {
            //Update the rates
            double summedRate = 0.0;
            uint totalCount = 0;
            var allNodes = GetAllNodes();
            foreach(var node in allNodes)
            {
                summedRate += node.MCPDevice.UpdateMessageRate();
                totalCount += node.MCPDevice.MessageCount;
            }

            String ioSummary = String.Format("Recv: {0}, Disp: {1}", IO.ToReceive, IO.ToDispatch);
            ActivityLog.Add(new BusActivity(totalCount, summedRate, ioSummary));
        };
    
        monitorNodeStateTimer.AutoReset = true;
        monitorNodeStateTimer.Interval = 1000;
        monitorNodeStateTimer.Elapsed += (sender, eargs) =>
        {
            var allNodes = GetAllNodes();
            foreach(var node in allNodes)
            {
                var mcpDev = node.MCPDevice;
                double sinceLastMessage = (DateTime.Now - mcpDev.LastMessageOn).TotalMilliseconds;
                if(mcpDev.State != CANNodeState.NOT_SET && mcpDev.PresenceInterval > 0 &&  sinceLastMessage > mcpDev.PresenceInterval + 100)
                {
                    mcpDev.State = CANNodeState.SILENT;
                } 
                else if(mcpDev.State == CANNodeState.RESPONDING)
                {
                    var timeSinceLastStatusRequest = (DateTime.Now - mcpDev.LastStatusRequest).TotalMilliseconds;
                    if(timeSinceLastStatusRequest > 500 && mcpDev.LastStatusRequest > mcpDev.LastStatusResponse)
                    {
                        mcpDev.State = CANNodeState.TRANSMITTING;
                    }
                }
            }
        };

        Ready += (sender, ready) =>
        {
            if (ready)
            {
                monitorNodeStateTimer.Start();
            }
            else
            {
                monitorNodeStateTimer.Stop();
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
    public void AddRemoteNode(ICANBusNode remoteNode)
    {
        byte rnid = remoteNode.MCPDevice.NodeID;
        if (rnid == 0 || rnid == MasterNode.NodeID)
        {
            throw new Exception(String.Format("Node ID {0} is not a valid ID for a remote node", rnid));
        }

        if (RemoteNodes.ContainsKey(rnid))
        {
            throw new ArgumentException(String.Format("Node {0} already added!", rnid));
        }
        remoteNode.IO = new MessageIO<ArduinoMessage>(0, 0); //Message IO with no throttling
        remoteNode.IO.MessageReceived += (sender, message) =>
        {
            try
            {
                remoteNode.RouteMessage(message); 
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                //TODO: What exactly?
            }
        };
        remoteNode.IO.MessageDispatched += (sender, eargs) =>
        {
            var msg = remoteNode.IO.LastMessageDispatched;
            if(msg != null)
            {
                try
                {
                    MasterNode.SendBusMessage(remoteNode.NodeID, msg);
                } 
                catch (Exception)
                {
                    //Console.WriteLine("Exception: {0}", e.Message);
                    //TODO: What exactly?
                }
            }
        };
        
        remoteNode.MCPDevice.Ready += (sender, ready) =>
        {
            NodeReady?.Invoke(remoteNode, ready);
            remoteNode.MCPDevice.Initialise();
        };

        remoteNode.MCPDevice.ErrorReceived += (sender, emsg) =>
        {
            NodeError?.Invoke(remoteNode, remoteNode.MCPDevice.LastError);
        };
        
        //Keep a list
        RemoteNodes[rnid] = remoteNode;
    }
    
    public void AddRemoteNode()
    {
        byte nid = (byte)(MasterNode.NodeID + RemoteNodes.Count + 1);
        AddRemoteNode(new CANBusNode(nid));
    }

    public List<ICANBusNode> GetAllNodes()
    {
        var l = new List<ICANBusNode> { this };
        l.AddRange(RemoteNodes.Values);
        return l;
    }
    
    public ICANBusNode GetNode(byte nodeID)
    {
        if (nodeID == MasterNode.NodeID)
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
    public override void Begin()
    {
        foreach(var remoteNode in RemoteNodes.Values)
        {
            if(remoteNode.Connection == null && Connection != null)
            {
                remoteNode.Connection = new ProxyConnection(Connection);
            }
            remoteNode.Begin();
        }

        base.Begin();
    }
    public override async Task End()
    {
        foreach(var remoteNode in RemoteNodes.Values)
        {
            await remoteNode.End();
        }
        
        await base.End();
    }
    #endregion

    #region Messaging
    public override bool RouteMessage(ArduinoMessage message)
    {
        return base.RouteMessage(message);
    }

    public void PingNode(byte nodeID)
    {
        if(nodeID == MasterNode.NodeID || nodeID == 0)
        {
            MasterNode.Ping();
        }
        if(nodeID != MasterNode.NodeID)
        {
            MasterNode.SendBusMessage(nodeID, MessageType.PING);
        }
    }

    public void ResetNode(byte nodeID)
    {
        if(nodeID == MasterNode.NodeID || nodeID == 0)
        {
            MasterNode.Reset();
        }
        if(nodeID != MasterNode.NodeID)
        {
            MasterNode.SendBusMessage(nodeID, MessageType.RESET);
        }
    }

    public void RaiseNodeError(byte nodeID, MCP2515.MCP2515ErrorCode ecode, UInt32 edata = 0)
    {
        var message = new ArduinoMessage(MessageType.ERROR_TEST);
        message.Add((byte)ecode);
        message.Add(edata);
        if(nodeID == MasterNode.NodeID || nodeID == 0)
        {
            MasterNode.SendMessage(message);
        }
        if(nodeID != MasterNode.NodeID)
        {
            MasterNode.SendBusMessage(nodeID, message);
        }
    }
    #endregion
}
