using System;
using System.Reflection.Metadata;
using System.Text;
using Chetch.Arduino.Boards;
using Chetch.Messaging;
using Microsoft.Extensions.Logging;

namespace Chetch.Arduino.Services;

public class CANBusService<T> : ArduinoService<T> where T : CANBusService<T>
{
    #region Constants
    public const String COMMAND_LIST_BUSSES = "list-busses";
    public const String COMMAND_SYNCHRONISE_BUS = "sync-bus";
    public const String COMMAND_NODES_STATUS = "nodes-status";
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
        AddCommand(COMMAND_SYNCHRONISE_BUS, "Sync a specific bus");
        base.AddCommands();
    }

    protected override bool HandleCommandReceived(ServiceCommand command, List<object> arguments, Message response)
    {
        int busIdx = 0;
        CANBusMonitor bm;
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
                var nodes = new List<CANBusNode>();
                nodes.Add(bm);
                nodes.AddRange(bm.RemoteNodes.Values);
                var sl = new List<String>();
                StringBuilder sb = new StringBuilder();
                foreach (var node in nodes)
                {
                    var mcp = node.MCPNode;
                    sb.AppendFormat("Node {0} ({1})", node.NodeID, node.IsReady ? "ready" : "not ready");
                    sb.AppendLine();
                    sb.AppendFormat(" - Status Flags = {0}", Utilities.Convert.ToBitString(mcp.StatusFlags));
                    sb.AppendLine();
                    sb.AppendFormat(" - Error Flags = {0}", Utilities.Convert.ToBitString(mcp.ErrorFlags));
                    sb.AppendLine();
                    sb.AppendFormat(" - TXErrorCount = {0}", mcp.TXErrorCount);
                    sb.AppendLine();
                    sb.AppendFormat(" - RXErrorCount = {0}", mcp.RXErrorCount);
                    sb.AppendLine();
                    sl.Add(sb.ToString());
                    sb.Clear();
                }
                response.AddValue("Nodes", sl);
                    
                return true;

            case COMMAND_SYNCHRONISE_BUS:
                if (arguments.Count > 0)
                {
                    busIdx = System.Convert.ToInt16(arguments[0].ToString());
                }
                if (busIdx < 0 || busIdx >= BusCount)
                {
                    throw new ArgumentException(String.Format("Index {0} is not valid", busIdx));
                }
                bm = GetBusMonitor(busIdx);
                bm.Synchronise();
                return true;

            default:
                return base.HandleCommandReceived(command, arguments, response);
        }       
    }
    #endregion
}
