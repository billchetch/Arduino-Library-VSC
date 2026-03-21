using System;
using XmppDotNet.Xml;

namespace Chetch.Arduino.Devices.Temperature;

public class DS18B20Array : ArduinoDevice
{
    #region consts and static
    public const float MIN_TEMP = -55.0f;
    public const float MAX_TEMP = 125.0f;

    public const float MIN_ACCURATE_TEMP = -10.0f;
    public const float MAX_ACCURATE_TEMP = 85.0f;

    public const float INCORRECT_READING = 85.0f; //over a sustained period of time
    public const float DISCONNECTION_READING = -127.0f;

    #endregion

    #region enum and Classes
    
    #endregion

    #region Properties
    
    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 1)]    
    public byte SensorCount 
    { 
        get { return sensorCount; } 
        internal set
        {
            if(value != sensorCount){
                sensorCount = value;
                temps.Clear();
                for(int i = 0; i < sensorCount; i++)
                {
                    temps.Add(0.0f);
                }
            }
        }
    }

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 2)]    
    public byte Resolution { get; internal set; } //tells us about accuracy and timing

    public float Temperature => temps.Count > 0 ? temps[0] : DISCONNECTION_READING;
    #endregion

    #region Events
    public EventHandler<List<float>>? ReadingUpdated;
    #endregion

    #region Fields
    byte sensorCount = 0;
    List<float> temps = new List<float>();

    #endregion

    #region Constructors
    public DS18B20Array(string sid, string? name = null) : base(sid, name)
    {
        //empty
    }
    #endregion

    #region Messaging
    public override ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        var retVal = base.HandleMessage(message);
        
        switch (message.Type)
        {
            case Messaging.MessageType.DATA:
                for(int i = 0; i < SensorCount; i++)
                {
                    temps[i] = message.Get<float>(i);
                }
                ReadingUpdated?.Invoke(this, temps);
                break;
        }
        return retVal;
    }
    #endregion

    #region Methods
    public float GetTemperature(int index)
    {
        return temps[index];
    }
    #endregion
}
