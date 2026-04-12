using System;
using System.Reflection;
using Chetch.Arduino.Devices;
using Chetch.Arduino.Devices.Comms;
using Chetch.Arduino.Devices.Displays;
using Chetch.Messaging;

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

    #region Report Updatable

    [ArduinoMessageMap(MessageType.DATA, 0)]
    public WMErrorCode WMError {get; internal set; }

    [ArduinoMessageMap(MessageType.DATA, 0)] //we don't assign (see AssignMessageValue beloe)
    public OperationalMode Mode => ModeSelector.SelectedItem;

    [ArduinoMessageMap(MessageType.DATA, 2)]
    public bool IsRunning { get; internal set; }

    [ArduinoMessageMap(MessageType.DATA, 3)]
    public UInt16 Duration {get; internal set;  }

    #endregion

    #endregion //end properties region

    #region Events
    public event EventHandler<bool>? Started;
    public event EventHandler<OperationalMode>? ModeSelected;
    #endregion

    #region Constructors
    public WaterMaker(byte nodeID) : base(nodeID, WATERMAKER_SID)
    {
        ModeSelector.Selected += (sender, mode) =>
        {
            ModeSelected?.Invoke(this, mode);  
        };

        FeederPump.Switched += (sender, on) =>
        {
            Started?.Invoke(this, on);
        };

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
    public override bool AssignMessageValue(PropertyInfo propertyInfo, object propertyValue, ArduinoMessage message)
    {
        if (propertyInfo.Name.Equals("Mode"))
        {
            //Note that as there is no set method for this property, without handling here it would later simply throw an exception 
            //which is just ignored (See base.AssignMessageValue)
            return true; 
        }
        
        return base.AssignMessageValue(propertyInfo, propertyValue, message);
    }


    #endregion
}
