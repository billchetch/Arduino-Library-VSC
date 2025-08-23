using System;
using Chetch.Arduino.Devices.Comms;


namespace Chetch.Arduino.Boards;

public class CANBusMonitor : ArduinoBoard
{
    #region Constants   
    public const String DEFAULT_BOARD_NAME = "canbusmon";
    #endregion

    #region Properties
    public MCP2515 BusMonitor { get; } = new MCP2515("busmon");

    #endregion

    #region Constructors
    public CANBusMonitor(String sid = DEFAULT_BOARD_NAME) : base(sid)
    {
        BusMonitor.MessageForwarded += (sender, eargs) =>
        {
            //eargs.
            Console.WriteLine("Forwarded message of priority {0} and type{1} from Node {2} and device {3} !", eargs.CanID.Priority, eargs.CanID.Messagetype, eargs.CanID.NodeID, eargs.CanID.Sender);
        };

        AddDevice(BusMonitor);
    }

    #endregion
}
