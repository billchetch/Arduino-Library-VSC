using System;
using Chetch.Arduino.Boards;
using Microsoft.Extensions.Logging;

namespace Chetch.Arduino.Services;

public class CANBusService<T> : ArduinoService<T> where T : CANBusService<T>
{

    #region Constructors
    public CANBusService(ILogger<T> Logger) : base(Logger)
    {
    }
    #endregion

    #region Methods
    public void AddBus(CANBusMonitor bus)
    {
        AddBoard(bus);
    }
    #endregion
}
