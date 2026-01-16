using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Chetch.Arduino.Connections;
using Chetch.Arduino.Devices;
using Chetch.Arduino.Devices.Comms;
using Chetch.Messaging;
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
    
    #endregion

    #region Properties
    public MCP2515Master MasterNode { get; } = new MCP2515Master(1);

    public MCP2515 MCPDevice => MasterNode; // for interface compliance

    public int BusSize => 1 + RemoteNodes.Count;

    public Dictionary<byte, ICANBusNode> RemoteNodes { get; } = new Dictionary<byte, ICANBusNode>();
    
    public uint BusMessageCount
    {
        get
        {
            uint mc = 0;
            var allNodes = GetAllNodes();
            foreach(var nd in allNodes)
            {
                mc += nd.MCPDevice.MessageCount;
            }
            return mc;
        }
    }

    public double BusMessageRate { get; internal set; } = 0.0;

    public String BusSummary
    {
        get
        {
            var s = new StringBuilder();
            if (IsReady)
            {
                //s.AppendFormat("Bus monitor {0}, {1} nodes out of {2} are ready!", SID, nodeReadyCount, BusSize);
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
    public EventHandler<bool>? NodeReady;

    public EventHandler<MCP2515.MCP2515ErrorCode>? NodeError;
    
    public EventHandler<MCP2515Master.BusMessageEventArgs>? BusMessageReceived;

    #endregion

    #region Fields
    System.Timers.Timer updateMessageRateTimer = new System.Timers.Timer();
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
                    //Millis and timestamp resolution
                    message.Populate<UInt32, byte>(canData);
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

            busNode.MCPDevice.UpdateMessageCount(eargs.CanID.Timestamp);
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
            var statusRequest = new ArduinoMessage(MessageType.STATUS_REQUEST);
            foreach(var remoteNode in RemoteNodes.Values)
            {
                statusRequest.Sender = remoteNode.MCPDevice.ID;
                MasterNode.SendBusMessage(remoteNode.NodeID, statusRequest);
            }
            MasterNode.RequestStatus();
        };

        updateMessageRateTimer.Interval = 1000;
        updateMessageRateTimer.AutoReset = true;
        updateMessageRateTimer.Elapsed += (sender, eargs) =>
        {
            UpdateBusMessageRate();
        };

        Ready += (sender, ready) =>
        {
            if (ready)
            {
                updateMessageRateTimer.Start();    
            } 
            else
            {
                updateMessageRateTimer.Stop();        
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
        remoteNode.IO = new MessageIO<ArduinoMessage>(); 
        remoteNode.IO.MessageReceived += (sender, message) =>
        {
            remoteNode.RouteMessage(message); 
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

    public void UpdateBusMessageRate()
    {
        //Update the rates
        double summedRate = 0.0;
        var allNodes = GetAllNodes();
        foreach(var node in allNodes)
        {
            node.MCPDevice.UpdateMessageRate();
            summedRate += node.MCPDevice.MessageRate;
        }

        BusMessageRate = summedRate;
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
    
    #endregion
}
