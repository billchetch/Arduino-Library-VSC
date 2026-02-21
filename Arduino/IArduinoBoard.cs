using System;
using Chetch.Arduino.Connections;
using Chetch.Messaging;

namespace Chetch.Arduino;

public interface IArduinoBoard
{

    byte ID { get; }

    IConnection? Connection { get; set; } 

    MessageIO<ArduinoMessage> IO {get; set; }

    bool RouteMessage(ArduinoMessage message);

    void Begin();

    Task End();

    bool HasDevice(byte id);

    ArduinoDevice GetDevice(byte id);

    void AddDevice(ArduinoDevice device);

    bool IsReady { get; }

    public event EventHandler<bool>? Ready;
}
