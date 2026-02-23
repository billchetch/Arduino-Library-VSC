using System;

namespace Chetch.Arduino;

public interface IArduinoDevice
{
    public ArduinoBoard Board { get; set; }

    bool IsReady { get; }

    public event EventHandler<bool>? Ready;

    public void RequestStatus();

    public void Ping();

    public void Initialise();

    public void Reset();

    public void Finalise();
}
