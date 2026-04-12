using System;
using Chetch.Arduino.Devices;
using Chetch.Arduino.Devices.Comms;
using Chetch.Arduino.Devices.Displays;

namespace Chetch.Arduino.Boards.Water;

public class WaterMaker : CANBusNode
{
    #region  Constants
    public const String WATERMAKER_SID = "watermaker";
    #endregion

    #region Enums and Classes
    public enum WMErrorCode : byte{
        NO_ERROR = 0,
        LOW_PRESSURE = 1,
        HIGH_PRESSURE = 2,
        FP_INCORRECT = 3,
        PP_INCORRECT = 4,
        LPS_INCORRECT = 5,
        HPS_INCORRECT = 6,
        CAN_BUS = 7, //Used for debugging
    };

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

    #region Report Updated
    public WMErrorCode WMError {get; }

    public OperationalMode Mode => ModeSelector.SelectedItem;

    public bool IsRunning { get; }

    public int Duration {get; }
    #endregion

    #endregion //end properties region

    #region Constructors
    public WaterMaker(byte nodeID) : base(nodeID, WATERMAKER_SID)
    {
        //IMPORTANT: Order that devices are added is important and should match those on the
        //Arduino Board as objects are mapped based on ID values and these are automatically assigned
        //based on order of adding device to board

        //NOTE: MCP and SerialPin devices added by base which makes ID values start at 10!

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

    #region Methods
    //Empty
    #endregion

    #region Messaging
    //Empty
    #endregion
}
