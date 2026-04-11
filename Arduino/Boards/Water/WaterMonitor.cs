using System;
using Chetch.Arduino.Devices.Comms.CAN;
using Chetch.Arduino.Devices.Temperature;
using Chetch.Arduino.Devices.Water;

namespace Chetch.Arduino.Boards.Water;

public class WaterMonitor : CANBusNode
{

    #region Properties
    public DS18B20Array Temp {get; } = new DS18B20Array("temp");

    public FlowMeter Flow {get; } = new FlowMeter("flowmeter");

    public TDSMeter TDS {get; } = new TDSMeter("tds");
    
    #endregion



    #region Constructors
    public WaterMonitor(byte nodeID) : base(nodeID)
    {
        

        AddDevice(Temp);
        AddDevice(Flow);
        AddDevice(TDS);
    }
    #endregion
}
