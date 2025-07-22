using System;
using System.Collections;
using Microsoft.EntityFrameworkCore.Storage.Json;

namespace Chetch.Arduino;

/// <summary>
/// Collection class for related arduino devices.  
/// There is no equipvalent on the actual arduino board.
/// This is purely a programging convenience
/// </summary>
public class ArduinoDeviceGroup : ICollection<ArduinoDevice>
{

    #region Properties
    public int Count => devices.Count;

    public bool IsReadOnly => throw new NotImplementedException();

    public String Name { get; internal set; } = "Unknown";

    public bool IsReady => deviceReadyCount == Count;

    #endregion

    #region Events
    public event EventHandler<bool>? Ready;

    #endregion

    #region Fields
    List<ArduinoDevice> devices = new List<ArduinoDevice>();
    int deviceReadyCount = 0;
    #endregion

    #region Constructors
    public ArduinoDeviceGroup(String name)
    {
        Name = name;
    }

    public ArduinoDeviceGroup() { }
    #endregion

    #region Collection methods
    public ArduinoDevice Get(String sid)
    {
        foreach (var dev in this)
        {
            if (dev.SID.Equals(sid)) return dev;
        }
        throw new Exception(String.Format("Cannot find device with string ID {0}", sid));
    }

    public ArduinoDevice Get(byte id)
    {
        foreach (var dev in this)
        {
            if (dev.ID == id) return dev;
        }
        throw new Exception(String.Format("Cannot find device with ID {0}", id));
    }

    virtual public void Add(ArduinoDevice device)
    {
        devices.Add(device);
        device.Ready += (sender, ready) =>
        {
            //Console.WriteLine("Device {0} fired Ready Event, ready: {1}", device.SID, ready);
            if (!ready && deviceReadyCount <= 0)
            {
                throw new Exception("Unexpected trigger of device ready");
            }
            if (deviceReadyCount >= Count && ready)
            {
                throw new Exception("Unexpected trigger of device ready");
            }

            bool prevReady = IsReady;
            deviceReadyCount += ready ? 1 : -1;
            if (prevReady != IsReady)
            {
                Ready?.Invoke(this, IsReady);
            }
        };
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
    #endregion

    #region Messaging
    public void RequestStatus()
    {
        foreach (var d in this)
        {
            d.RequestStatus();
        }
    }
    #endregion
}
