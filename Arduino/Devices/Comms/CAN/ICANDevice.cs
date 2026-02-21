using System;
using Chetch.Arduino.Boards;

namespace Chetch.Arduino.Devices.Comms.CAN;

public interface ICANDevice
{
    byte NodeID { get; }

    CANNodeState State {get; }

    EventHandler<CANNodeStateChange>? StateChanged { get; set; }
}
