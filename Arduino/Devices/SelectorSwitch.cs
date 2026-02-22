using System;
using Chetch.Messaging;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Chetch.Arduino.Devices;

/*
IMPORTANT: Beware of the SelectItemIndexStart value of 1 as this makes the selectedIndex start at 1 from the list of items
*/

public class SelectorSwitch<T> : SwitchDevice where T : struct
{
    #region Properties
    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 4)] //Initial value
    public byte FirstPin { get; set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 5)] //Initial value
    public byte LastPin { get; set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 6)] //Initial value
    [ArduinoMessageMap(Messaging.MessageType.DATA, 2)] //Operational value (will change depending on switch activity)
    public byte SelectedPin 
    { 
        get { return selectedPin; } 
        set
        {
            if(value < FirstPin || value > LastPin)
            {
                throw new Exception(String.Format("Cannot set {0} selected pin to {1} as it lies outside range of First Pin {2} to  Last Pin {3}", SID, value, FirstPin, LastPin));
            }
            if(value != selectedPin)
            {
                selectedPin = value;
                if(items.Count == 0){
                    var item = System.Convert.ChangeType(SelectedIndex, typeof(T));
                    SelectedItem = (T)item;
                } else
                {
                    SelectedItem = items[SelectedIndex];
                }
                Selected?.Invoke(this, SelectedItem);
            }
        }
    }

    public int SelectedIndex => SelectedPin == 0 ? -1 : (SelectedPin - FirstPin) + SelectItemIndexStart;

    public T SelectedItem { get; private set; }

    public int SelectItemIndexStart = 1;
    #endregion

    #region Events
    public event EventHandler<T>? Selected;
    #endregion

    #region Fields
    byte selectedPin = 0;

    List<T> items = new List<T>();
    #endregion

    #region Constructors
    public SelectorSwitch(byte id, String sid, String? name = null) : base(id, sid, name)
    {
        if(typeof(T).IsEnum)
        {
            var vals = Enum.GetValues(typeof(T));
            foreach(var val in vals)
            {
                items.Add((T)val);
            }
        }
        else if(typeof(T) == typeof(int))
        {
            //don't need to do anything
        }
        else
        {
            throw new NotImplementedException(String.Format("Not yet implemented for type: {0}", typeof(T)));
        }
    }
    
    public SelectorSwitch(String sid, String? name = null) : this(0, sid, name)
    {}

    public SelectorSwitch(String sid, SwitchMode mode, String? name = null) : this(sid, name)
    {
        Mode = mode;
    }
    #endregion

    #region Methods

    #endregion

    #region Messaging

    #endregion
}
