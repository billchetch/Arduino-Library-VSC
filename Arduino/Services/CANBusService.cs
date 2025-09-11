using System;
using Chetch.Arduino.Boards;
using Chetch.Messaging;
using Microsoft.Extensions.Logging;

namespace Chetch.Arduino.Services;

public class CANBusService<T> : ArduinoService<T> where T : CANBusService<T>
{
    #region Constants
    public const String COMMAND_LIST_BUSSES = "list-busses";
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
    }
    #endregion

    #region Client issued Command handling
    protected override void AddCommands()
    {
        AddCommand(COMMAND_LIST_BUSSES, "Lists current busses and their ready status");
        base.AddCommands();
    }

    protected override bool HandleCommandReceived(ServiceCommand command, List<object> arguments, Message response)
    {
        switch (command)
        {
            case COMMAND_LIST_BUSSES:
                var bl = new List<String>();
                foreach (var bus in Boards)
                {
                    if (bus is CANBusMonitor)
                    {
                        bl.Add(((CANBusMonitor)bus).BusSummary);
                    }
                }
                response.AddValue("Busses", bl);
                return true;
                
            default:
                return base.HandleCommandReceived(command, arguments, response);
        }       
    }
    #endregion
}
