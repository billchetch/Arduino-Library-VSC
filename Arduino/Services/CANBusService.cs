using System;
using System.Reflection.Metadata;
using Chetch.Arduino.Boards;
using Chetch.Messaging;
using Microsoft.Extensions.Logging;

namespace Chetch.Arduino.Services;

public class CANBusService<T> : ArduinoService<T> where T : CANBusService<T>
{
    #region Constants
    public const String COMMAND_LIST_BUSSES = "list-busses";
    public const String COMMAND_SYNCHRONISE_BUS = "sync-bus";
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

            case COMMAND_SYNCHRONISE_BUS:
                if (arguments.Count == 0)
                {
                    throw new ArgumentException("No bus specified");
                }
                int busIdx = System.Convert.ToInt16(arguments[0].ToString());
                if (busIdx < 0 || busIdx >= BusCount)
                {
                    throw new ArgumentException(String.Format("Index {0} is not valid", busIdx));
                }
                var bm = GetBusMonitor(busIdx);
                bm.MasterNode.SynchroniseBus();
                return true;
                
            default:
                return base.HandleCommandReceived(command, arguments, response);
        }       
    }
    #endregion
}
