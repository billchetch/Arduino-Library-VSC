using System;
using Chetch.Arduino.Devices.Comms;


namespace Chetch.Arduino.Boards;

public class CANBusMonitor : ArduinoBoard
{
    #region Constants   
    public const String DEFAULT_BOARD_NAME = "canbusmon";
    #endregion

    #region Properties
    public MCP2515 Master { get; } = new MCP2515("master");

    #endregion

    #region Constructors
    public CANBusMonitor(String sid = DEFAULT_BOARD_NAME) : base(sid)
    {
        AddDevice(Master);
    }
    
    #endregion
}
