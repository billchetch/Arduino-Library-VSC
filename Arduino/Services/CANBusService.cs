using System;
using System.Net.NetworkInformation;
using System.Reflection.Metadata;
using System.Text;
using Chetch.Arduino.Boards;
using Chetch.Arduino.Devices.Comms;
using Chetch.Messaging;
using Chetch.Messaging.Attributes;
using Microsoft.Extensions.Logging;
using XmppDotNet.Xmpp.Jingle;

namespace Chetch.Arduino.Services;

public class CANBusService<T> : ArduinoService<T> where T : CANBusService<T>
{
    #region Constants
    public const String COMMAND_LIST_BUSSES = "list-busses";
    public const String COMMAND_BUS_ACTIVITY = "bus-activity";
    public const String COMMAND_NODE_STATE_CHANGES = "state-changes";
    public const String COMMAND_NODES_STATUS = "nodes-status";
    public const String COMMAND_NODE_ERRORS = "node-errors";
    public const String COMMAND_ERROR_COUNTS = "error-counts";
    public const String COMMAND_PING_NODE = "ping-node";
    public const String COMMAND_STAT_NODE = "stat-node";
    public const String COMMAND_INITIALISE_NODE = "init-node";
    public const String COMMAND_RESET_NODE = "reset-node";
    public const String COMMAND_RAISE_ERROR = "raise-error";
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
        bus.NodeReady += (sender, ready) =>
        {
            ICANBusNode node = (ICANBusNode)sender;
            Logger.LogInformation("Node {0} is ready: {1}", node.NodeID, ready);
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
        AddCommand(COMMAND_BUS_ACTIVITY, "Show recent activity for a particular <?bus>");
        AddCommand(COMMAND_NODE_STATE_CHANGES, "Log of state changes for a paritcular <?bus>");
        AddCommand(COMMAND_INITIALISE_NODE, "Init a specific <?node> on a <?bus>");
        AddCommand(COMMAND_PING_NODE, "Ping a <?node> on a <?bus>");
        AddCommand(COMMAND_STAT_NODE, "Request status from a <?node> on a <?bus>");
        AddCommand(COMMAND_RESET_NODE, "Reset a <?node> on a <?bus>");
        AddCommand(COMMAND_NODES_STATUS, "List the status of the nodes on a particular <bus?>");
        AddCommand(COMMAND_NODE_ERRORS, "List the errors of a particular <node> on a <bus?>");
        AddCommand(COMMAND_ERROR_COUNTS, "Number of errors per type on a particular <bus?>");
        AddCommand(COMMAND_RAISE_ERROR, "Target <node> to raise <?error> on <?bus>");
        base.AddCommands();
    }

    CANBusMonitor getBusMonitor(List<Object> arguments, int minArgs = 0)
    {
        int busIdx = 0;
        if (arguments.Count > minArgs)
        {
            busIdx = System.Convert.ToInt16(arguments.Last().ToString());
        }
        if (busIdx < 0 || busIdx >= BusCount)
        {
            throw new ArgumentException(String.Format("Index {0} is not valid", busIdx));
        }
        return GetBusMonitor(busIdx);
    }

    protected override bool HandleCommandReceived(ServiceCommand command, List<object> arguments, Message response)
    {
        CANBusMonitor bm;
        StringBuilder sb;
        List<ICANBusNode> nodes;
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

            case COMMAND_BUS_ACTIVITY:
                bm = getBusMonitor(arguments);
                foreach(var s in bm.ActivityLog)
                {
                    response.AddValue(String.Format("Activity Entry {0}: ", n++), s.ToString());
                }
                return true;

            case COMMAND_NODE_STATE_CHANGES:
                bm = getBusMonitor(arguments);
                foreach(var s in bm.StateChanges)
                {
                    response.AddValue(String.Format("State Change {0}: ", n++), s.ToString());
                }
                return true;

            case COMMAND_NODES_STATUS:
                bm = getBusMonitor(arguments);

                //Master node first
                nodes = bm.GetAllNodes();
                sb = new StringBuilder();
                foreach (var node in nodes)
                {
                    var mcp = node.MCPDevice;
                    if (mcp.IsReady)
                    {
                        sb.AppendFormat(" - Bus Message Count and Rate = {0} ... {1}mps", mcp.MessageCount, mcp.MessageRate);
                        sb.AppendLine();
                        sb.AppendFormat(" - Status Flags = {0}", Utilities.Convert.ToBitString(mcp.StatusFlags));
                        sb.AppendLine();
                        sb.AppendFormat(" - Error Flags = {0}", Utilities.Convert.ToBitString(mcp.ErrorFlags));
                        sb.AppendLine();
                        sb.AppendFormat(" - TXErrorCount / RXErrorCount = {0} / {1}", mcp.TXErrorCount, mcp.RXErrorCount);
                        sb.AppendLine();
                        sb.AppendFormat(" - Last Error = {0}", mcp.LastError);
                        sb.AppendLine();
                        sb.AppendFormat(" - Last Error Data = {0}", Utilities.Convert.ToBitString(mcp.LastErrorData, "-"));
                        sb.AppendLine();
                        sb.AppendFormat(" - Last Error On = {0}", mcp.LastErrorOn.ToString("s"));
                        sb.AppendLine();
                        sb.AppendFormat(" - Error Code Flags = {0}", Utilities.Convert.ToBitString(mcp.ErrorCodeFlags, "-"));
                        sb.AppendLine();
                        sb.AppendFormat(" - Error Log Writes = {0}", mcp.ErrorLog.WritesCount + (mcp.ErrorLog.IsFull ? " (full)" : ""));
                        sb.AppendLine();
                        sb.AppendFormat(" - Last Ready On = {0}", mcp.LastReadyOn.ToString("s"));
                        sb.AppendLine();
                        sb.AppendFormat(" - Last Presence On = {0}", mcp.LastPresenceOn.ToString("s"));
                        sb.AppendLine();
                        sb.AppendFormat(" - Last Status Response = {0}", mcp.LastStatusResponse.ToString("s"));
                        sb.AppendLine();
                        sb.AppendFormat(" - Last Message On = {0}", mcp.LastMessageOn.ToString("s"));
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.Append("Not Ready");
                    }
                    response.AddValue(String.Format("N{0} {1}", node.MCPDevice.NodeID, node.NodeState), sb.ToString());
                    sb.Clear();
                }    
                return true;

            case COMMAND_NODE_ERRORS:
                if (arguments.Count > 0)
                {
                    nodeID = System.Convert.ToByte(arguments[0].ToString());
                } 
                bm = getBusMonitor(arguments, 1);
                
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
                bm = getBusMonitor(arguments, 1);
                //bm.InitialiseNode(nodeID);
                return true;

            case COMMAND_STAT_NODE:
                if (arguments.Count > 0)
                {
                    nodeID = System.Convert.ToByte(arguments[0].ToString());
                }
                bm = getBusMonitor(arguments, 1);
                //bm.RequestNodeStatus(nodeID); //get them all
                if(nodeID != 0){
                    MessageParser.Parse(response, bm.GetNode(nodeID).MCPDevice);
                }
                return true;

            case COMMAND_PING_NODE:
                if (arguments.Count > 0)
                {
                    nodeID = System.Convert.ToByte(arguments[0].ToString());
                }
                bm = getBusMonitor(arguments, 1);
                bm.PingNode(nodeID);
                return true;

            case COMMAND_RESET_NODE:
                if (arguments.Count > 0)
                {
                    nodeID = System.Convert.ToByte(arguments[0].ToString());
                }
                bm = getBusMonitor(arguments, 1);
                bm.ResetNode(nodeID, MCP2515.ResetRegime.FULL_RESET);
                return true;

            case COMMAND_RAISE_ERROR:
                if (arguments.Count == 0)
                {
                    throw new ArgumentException("A node and must be specified on which to raise an error");
                }
                nodeID = System.Convert.ToByte(arguments[0].ToString());
                MCP2515.MCP2515ErrorCode ecode = MCP2515.MCP2515ErrorCode.DEBUG_ASSERT;
                if (arguments.Count > 1)
                {
                    ecode = (MCP2515.MCP2515ErrorCode)System.Convert.ToByte(arguments[1].ToString());
                }        
                bm = getBusMonitor(arguments, arguments.Count > 1 ? 2 : 1);
                bm.RaiseNodeError(nodeID, ecode);
                return true;

            case COMMAND_ERROR_COUNTS:
                bm = getBusMonitor(arguments);
                nodes = bm.GetAllNodes();
                sb = new StringBuilder();
                foreach (var node in nodes)
                {
                    var mcp = node.MCPDevice;
                    foreach(var kv in mcp.ErrorCounts)
                    {
                        sb.AppendFormat("{0} = {1}", kv.Key.ToString(), kv.Value);
                        sb.AppendLine();
                    }
                    response.AddValue(String.Format("Node {0} Error Counts", node.MCPDevice.NodeID), sb.ToString());
                    sb.Clear();
                }

                return true;

            default:
                return base.HandleCommandReceived(command, arguments, response);
        }       
    }
    #endregion
}
