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
    public byte ID { get; set; } = ArduinoBoard.DEFAULT_BOARD_ID;
    public bool IsConnected => Connection != null && Connection.IsConnected;
    public LocalSocket Connection { get; internal set; }

    public String Name { get; set; } = "Virtual";

    public byte DeviceCount => 0;
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
        Connection.Connected += (sender, connected) =>
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
                ArduinoMessage response = new ArduinoMessage();
                bool respond = HandleMessageReceived(message, response);
                MessageReceived?.Invoke(this, message);
                if (respond)
                {
                    SendMessage(response);
                }
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
        io.Start();
        Connection.StartListening();
    }

    public void End()
    {
        Connection?.StopListening();
        io.Stop();
    }

    virtual protected bool HandleMessageReceived(ArduinoMessage message, ArduinoMessage response)
    {
        bool handled = false;
        if (message.Target == ID)
        {
            switch (message.Type)
            {
                case MessageType.STATUS_REQUEST:
                    response.Type = MessageType.STATUS_RESPONSE;
                    response.Add(Name);
                    response.Add(DateTime.Now.Millisecond);
                    response.Add(DeviceCount);
                    response.Add(0);
                    handled = true;
                    break;
            }

            response.Target = message.Target;
        }
        return handled;
    }
    
    public void SendMessage(ArduinoMessage message)
    {
        if (!IsConnected)
        {
            throw new Exception("Board is not connected");
        }

        if (message.Sender == ArduinoMessage.NO_SENDER)
        {
            message.Sender = ID; //this is the default
        }

        //adds this message to the IO out queue which will then send based on the MessageDispatched event
        //See (io configuration in Begin method above)
        io.Add(message);
    }
}
