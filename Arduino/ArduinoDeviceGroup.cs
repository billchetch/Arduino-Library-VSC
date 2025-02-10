using System;
using System.Collections;

namespace Chetch.Arduino;

public class ArduinoDeviceGroup : ICollection<ArduinoDevice>
{

    public int Count => throw new NotImplementedException();

    public bool IsReadOnly => throw new NotImplementedException();

    public String Name { get; internal set; } = "Unknown";

    List<ArduinoDevice> devices = new List<ArduinoDevice>();

    public ArduinoDeviceGroup(String name)
    {
        Name = name;
    }

    public ArduinoDeviceGroup(){}


    public ArduinoDevice Get(String sid)
    {
        foreach(var dev in this)
        {
            if(dev.SID.Equals(sid))return dev;
        }
        throw new Exception(String.Format("Cannot find device with string ID {0}", sid));
    }

     public ArduinoDevice Get(byte id)
    {
        foreach(var dev in this)
        {
            if(dev.ID == id)return dev;
        }
        throw new Exception(String.Format("Cannot find device with ID {0}", id));
    }

    virtual public void Add(ArduinoDevice device)
    {
        devices.Add(device);
    }

    public void Clear()
    {
        devices.Clear();
    }

    public bool Contains(ArduinoDevice device)
    {
        return devices.Contains(device);
    }

    public void CopyTo(ArduinoDevice[] array, int arrayIndex)
    {
        devices.CopyTo(array, arrayIndex);
    }

    public IEnumerator<ArduinoDevice> GetEnumerator()
    {
       return devices.GetEnumerator();
    }

    public bool Remove(ArduinoDevice device)
    {
        return devices.Remove(device);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
