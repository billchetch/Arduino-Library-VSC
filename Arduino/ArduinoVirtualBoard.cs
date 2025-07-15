using System;
using Chetch.Arduino;
using Chetch.Messaging;
using Chetch.Utilities;

namespace Chetch.Arduino;

public class ArduinoVirtualBoard
{
    #region Events
    public event EventHandler<ArduinoMessage>? MessageReceived;
    public event EventHandler<ArduinoMessage>? MessageSent;
    
    public event ErrorEventHandler? ExceptionThrown;
    #endregion

    #region Properties
    public LocalSocket Connection { get; internal set; }
    #endregion

    #region Fields
    MessageIO<ArduinoMessage> io = new MessageIO<ArduinoMessage>(Frame.FrameSchema.SMALL_SIMPLE_CHECKSUM, MessageEncoding.SYSTEM_DEFINED);
    #endregion

    public ArduinoVirtualBoard(String path)
    {
        if (path == null)
        {
            throw new Exception("No path for Arduino Local Socket connection");
        }
        Connection = new LocalSocket(path);
        Connection.Connected += (sender, skt) =>
        {
            Thread.Sleep(2000);  //Pauses simulates Serial connection
        };
        Connection.DataReceived += (sender, data) =>
        {
            try
            {
                io.Add(data);
            }
            catch (Exception e)
            {
                ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(e));
            }
        };
        io.ExceptionThrown += ExceptionThrown;
        io.MessageReceived += (sender, message) =>
        {
            try
            {
                HandleMessageReceived(message);
                MessageReceived?.Invoke(this, message);
            }
            catch (Exception e)
            {
                ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(e));
            }
        };
        io.MessageDispatched += (sender, bytes) =>
        {
            try
            {
                Connection.SendData(bytes);
            }
            catch (Exception e)
            {
                ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(e));
            }
        };
    }

    public void Begin()
    {
        Connection.StartListening();

    }

    public void End()
    {
        Connection?.StopListening();
    }

    virtual protected bool HandleMessageReceived(ArduinoMessage message)
    {
        switch (message.Type)
        {
            case MessageType.STATUS_REQUEST:
                break;  
        }
        return false;
    }
}
