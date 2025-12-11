using System;
using System.Net.NetworkInformation;
using System.Reflection.Metadata;
using System.Text;
using Chetch.Arduino.Boards;
using Chetch.Messaging;
using Microsoft.Extensions.Logging;
using XmppDotNet.Xmpp.Jingle;

namespace Chetch.Arduino.Services;

public class CANBusService<T> : ArduinoService<T> where T : CANBusService<T>
{
    #region Constants
    public const String COMMAND_LIST_BUSSES = "list-busses";
    public const String COMMAND_NODES_STATUS = "nodes-status";
    public const String COMMAND_NODE_ERRORS = "node-errors";
    public const String COMMAND_ERROR_COUNTS = "error-counts";
    public const String COMMAND_PING_NODE = "ping-node";
    public const String COMMAND_INITIALISE_NODE = "init-node";
    public const String COMMAND_RESET_NODE = "reset-node";
    #endregion

    #region Properties
    public int BusCount { get; internal set; } = 0;
    #endregion

    #region Constructors
    public CANBusService(ILogger<T> Logger) : base(Logger)
    {
    }
    #endregion

    #region Methods
    public void AddBusMonitor(CANBusMonitor bus)
    {
        bus.NodesReady += (Senders, ready) =>
        {
            if (ready)
            {
                Logger.LogInformation(0, "All {0} nodes of bus {1} are ready", bus.BusSize, bus.SID);
            }
        };
        AddBoard(bus);
        BusCount++;
    }

    public List<CANBusMonitor> GetBusMonitors()
    {
        List<CANBusMonitor> bl = new List<CANBusMonitor>();
        foreach (var bus in Boards)
        {
            if (bus is CANBusMonitor)
            {
                bl.Add((CANBusMonitor)bus);
            }
        }
        return bl;
    }

    public CANBusMonitor GetBusMonitor(int busIdx)
    {
        var l = GetBusMonitors();
        return l[busIdx];
    }
    #endregion

    #region Client issued Command handling
    protected override void AddCommands()
    {
        AddCommand(COMMAND_LIST_BUSSES, "Lists current busses and their ready status");
        AddCommand(COMMAND_INITIALISE_NODE, "Init a specific <?node> on a <?bus>");
        AddCommand(COMMAND_PING_NODE, "Ping a <?node> on a <?bus>");
        AddCommand(COMMAND_RESET_NODE, "Reset a <?node> on a <?bus>");
        AddCommand(COMMAND_NODES_STATUS, "List the status of the nodes on a particular <bus?>");
        AddCommand(COMMAND_NODE_ERRORS, "List the errors of a particular <node> on a <bus?>");
        AddCommand(COMMAND_ERROR_COUNTS, "Number of errors per type on a particular <bus?>");
        base.AddCommands();
    }

    protected override bool HandleCommandReceived(ServiceCommand command, List<object> arguments, Message response)
    {
        int busIdx = 0;
        CANBusMonitor bm;
        StringBuilder sb;
        List<CANBusNode> nodes;
        byte nodeID = 0;
        int n = 0;
        switch (command.Command)
        {
            case COMMAND_LIST_BUSSES:
                var bl = new List<String>();
                foreach (var bus in GetBusMonitors())
                {
                    bl.Add(bus.BusSummary);
                }
                response.AddValue("Busses", bl);
                return true;

            case COMMAND_NODES_STATUS:
                if (arguments.Count > 0)
                {
                    busIdx = System.Convert.ToInt16(arguments[0].ToString());
                }
                if (busIdx < 0 || busIdx >= BusCount)
                {
                    throw new ArgumentException(String.Format("Index {0} is not valid", busIdx));
                }
                bm = GetBusMonitor(busIdx);

                //Master node first
                nodes = bm.GetAllNodes();
                sb = new StringBuilder();
                foreach (var node in nodes)
                {
                    var mcp = node.MCPNode;
                    if (node.IsReady)
                    {
                        sb.AppendFormat(" - Bus Message Count = {0}", node.BusMessageCount);
                        sb.AppendLine();
                        sb.AppendFormat(" - Status Flags = {0}", Utilities.Convert.ToBitString(mcp.StatusFlags));
                        sb.AppendLine();
                        sb.AppendFormat(" - Error Flags = {0}", Utilities.Convert.ToBitString(mcp.ErrorFlags));
                        sb.AppendLine();
                        sb.AppendFormat(" - TXErrorCount = {0}", mcp.TXErrorCount);
                        sb.AppendLine();
                        sb.AppendFormat(" - RXErrorCount = {0}", mcp.RXErrorCount);
                        sb.AppendLine();
                        sb.AppendFormat(" - Last Error = {0}", mcp.LastError);
                        sb.AppendLine();
                        sb.AppendFormat(" - Last Error Data = {0}", Utilities.Convert.ToBitString(mcp.LastErrorData, "-"));
                        sb.AppendLine();
                        sb.AppendFormat(" - Last Error On = {0}", mcp.LastErrorOn.ToString("s"));
                        sb.AppendLine();
                        sb.AppendFormat(" - Error Code Flags = {0}", Utilities.Convert.ToBitString(mcp.ErrorCodeFlags, "-"));
                        sb.AppendLine();
                        sb.AppendFormat(" - Error Log Count = {0}", mcp.ErrorLog.Count);
                        sb.AppendLine();
                        sb.AppendFormat(" - Last Presence On = {0}", mcp.LastPresenceOn.ToString("s"));
                        sb.AppendLine();
                        sb.AppendFormat(" - Last Status Response = {0}", mcp.LastStatusResponse.ToString("s"));
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.Append("Not Ready");
                    }
                    response.AddValue(String.Format("Node {0} Status", node.NodeID), sb.ToString());
                    sb.Clear();
                }    
                return true;

            case COMMAND_NODE_ERRORS:
                if (arguments.Count > 0)
                {
                    nodeID = System.Convert.ToByte(arguments[0].ToString());
                }
                if (arguments.Count > 1)
                {
                    busIdx = System.Convert.ToInt16(arguments[0].ToString());
                }
                if (busIdx < 0 || busIdx >= BusCount)
                {
                    throw new ArgumentException(String.Format("Index {0} is not valid", busIdx));
                }
                bm = GetBusMonitor(busIdx);
                var nd = bm.GetNode(nodeID);
                foreach(var s in nd.ErrorLog)
                {
                    response.AddValue(String.Format("Log Entry {0}", n++), s.Summary);
                }
                return true;

            case COMMAND_INITIALISE_NODE:
                if (arguments.Count > 0)
                {
                    nodeID = System.Convert.ToByte(arguments[0].ToString());
                }
                if (arguments.Count > 1)
                {
                    busIdx = System.Convert.ToInt16(arguments[1].ToString());
                }
                if (busIdx < 0 || busIdx >= BusCount)
                {
                    throw new ArgumentException(String.Format("Index {0} is not valid", busIdx));
                }
                bm = GetBusMonitor(busIdx);
                if(nodeID == 0){
                    bm.InitialiseNodes();
                } 
                else 
                {
                    bm.InitialiseNode(nodeID);
                }
                return true;

            case COMMAND_PING_NODE:
                if (arguments.Count > 0)
                {
                    nodeID = System.Convert.ToByte(arguments[0].ToString());
                }
                if (arguments.Count > 1)
                {
                    busIdx = System.Convert.ToInt16(arguments[1].ToString());
                }
                if (busIdx < 0 || busIdx >= BusCount)
                {
                    throw new ArgumentException(String.Format("Index {0} is not valid", busIdx));
                }
                bm = GetBusMonitor(busIdx);
                if(nodeID == 0){
                    bm.PingNodes();
                } 
                else 
                {
                    bm.PingNode(nodeID);
                }
                return true;

            case COMMAND_RESET_NODE:
                if (arguments.Count > 0)
                {
                    nodeID = System.Convert.ToByte(arguments[0].ToString());
                }
                if (arguments.Count > 1)
                {
                    busIdx = System.Convert.ToInt16(arguments[1].ToString());
                }
                if (busIdx < 0 || busIdx >= BusCount)
                {
                    throw new ArgumentException(String.Format("Index {0} is not valid", busIdx));
                }
                bm = GetBusMonitor(busIdx);
                if(nodeID == 0){
                    bm.ResetNodes();
                } 
                else 
                {
                    bm.ResetNode(nodeID);
                }
                return true;

            case COMMAND_ERROR_COUNTS:
                if (arguments.Count > 0)
                {
                    busIdx = System.Convert.ToInt16(arguments[0].ToString());
                }
                if (busIdx < 0 || busIdx >= BusCount)
                {
                    throw new ArgumentException(String.Format("Index {0} is not valid", busIdx));
                }
                bm = bm = GetBusMonitor(busIdx);
                nodes = new List<CANBusNode>();
                nodes.Add(bm);
                nodes.AddRange(bm.RemoteNodes.Values);
                sb = new StringBuilder();
                foreach (var node in nodes)
                {
                    var mcp = node.MCPNode;
                    foreach(var kv in mcp.ErrorCounts)
                    {
                        sb.AppendFormat("{0} = {1}", kv.Key.ToString(), kv.Value);
                        sb.AppendLine();
                    }
                    response.AddValue(String.Format("Node {0} Error Counts", node.NodeID), sb.ToString());
                    sb.Clear();
                }

                return true;

            default:
                return base.HandleCommandReceived(command, arguments, response);
        }       
    }
    #endregion
}
