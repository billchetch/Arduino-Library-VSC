using System;
using Chetch.Arduino.Devices;
using Chetch.Arduino.Devices.Comms;
using Chetch.Arduino.Devices.Displays;

namespace Chetch.Arduino.Boards;

public class WaterMaker : CANBusNode
{
    #region  Constants
    public const String WATERMAKER_SID = "watermaker";
    public const byte DEFAULT_NODE_ID = 2;
    #endregion

    #region Enums and Classes
    public enum OperationalMode : byte
    {
        NOT_SET,
        MAKE_WATER,
        EXPEL_AIR,
        RINSE,
    }
    #endregion

    #region Properties
    /*
    Note that devices here could be outsourced to other nodes so this is a basic Watermaker
    and hence probably has more devices than later developments...
    */
    public GenericDisplay Display { get; } = new GenericDisplay();

    #region Inputs
    public SelectorSwitch<OperationalMode> ModeSelector { get; } = new SelectorSwitch<OperationalMode>("opmode");

    public PassiveSwitch StartButton { get; } = new PassiveSwitch("start");

    public PassiveSwitch LPS {get; } = new PassiveSwitch("lps");

    public PassiveSwitch HPS {get; } = new PassiveSwitch("hps");
    #endregion

    #region Outputs
    public ActiveSwitch SaltWaterValve {get; } = new ActiveSwitch("saltwater");
    
    public ActiveSwitch FreshWaterValve {get; } = new ActiveSwitch("freshwater");

    public ActiveSwitch FeederPump {get; } = new ActiveSwitch("feeder");

    public ActiveSwitch PressurePump {get; } = new ActiveSwitch("pressure");
    #endregion


    public OperationalMode Mode {get; internal set; } = OperationalMode.NOT_SET;
    #endregion

    #region Constructors
    public WaterMaker(byte nodeID = DEFAULT_NODE_ID) : base(nodeID, WATERMAKER_SID)
    {
        //IMPORTANT: Order that devices are added is important and should match those on the
        //Arduino Board as objects are mapped based on ID values and these are automatically assigned
        //based on order of adding device to board

        //NOTE: MCP and SerialPin devices added by base

        //Display
        AddDevice(Display);

        //Inputs
        AddDevice(ModeSelector);
        AddDevice(StartButton);
        AddDevice(LPS);
        AddDevice(HPS);

        //Outputs
        AddDevice(SaltWaterValve);
        AddDevice(FreshWaterValve);
        AddDevice(FeederPump);
        AddDevice(PressurePump);
        
    }
    #endregion

    #region Messaging
    public override bool RouteMessage(ArduinoMessage message)
    {
        return base.RouteMessage(message);
    }
    #endregion
}
