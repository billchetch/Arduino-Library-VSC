using System;
using System.Xml;
using Chetch.Arduino;
using Chetch.Messaging;
using Chetch.Utilities;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Chetch.Arduino;

public class ArduinoVirtualBoard
{
    #region Constants
    #endregion

    #region Classes and Enums
    public class Regime
    {
        public class RegimeItem
        {
            public ArduinoMessage? Message { get; internal set; }

            public int Delay { get; internal set; } = 0;

            public RegimeItem(ArduinoMessage message)
            {
                Message = message;
                Delay = 0;
            }

            public RegimeItem(int delay)
            {
                Message = null;
                Delay = delay;
            }
        }

        public const int DEFAULT_DELAY = 100;

        public String Name { get; internal set; }

        public int DefaultDelay { get; set; } = DEFAULT_DELAY;

        public int RepeatCount { get; set; } = 0;

        public List<RegimeItem> Items = new List<RegimeItem>();

        public Regime(String name, int defaultDelay = DEFAULT_DELAY, int repeatCount = 0)
        {
            Name = name;
            DefaultDelay = defaultDelay;
            RepeatCount = repeatCount;
        }

        public void AddMessage(MessageType messageType, byte target, params Object[] args)
        {
            var message = new ArduinoMessage(messageType);
            message.Target = target;
            message.Sender = target;

            foreach (var arg in args)
            {
                message.Add(arg);
            }
            Items.Add(new RegimeItem(message));
        }

        public void AddDelay(int delay)
        {
            Items.Add(new RegimeItem(delay));
        }
    }
    #endregion

    #region Events
    public event EventHandler<ArduinoMessage>? MessageReceived;
    public event EventHandler<ArduinoMessage>? MessageSent;

    public event EventHandler<bool>? Ready;

    public event ErrorEventHandler? ExceptionThrown;
    
    #endregion

    #region Properties
    public byte ID { get; set; } = ArduinoBoard.DEFAULT_BOARD_ID;
    public bool IsConnected => Connection != null && Connection.IsListening;
    public bool IsReady => IsConnected && statusRequestReceived;
    public LocalSocket Connection { get; internal set; }

    public String Name { get; set; } = "Virtual";

    public byte DeviceCount => 0;
    #endregion

    #region Fields
    MessageIO<ArduinoMessage> io = new MessageIO<ArduinoMessage>(Frame.FrameSchema.SMALL_SIMPLE_CHECKSUM, MessageEncoding.SYSTEM_DEFINED);

    bool statusRequestReceived = false;

    List<Regime> regimes = new List<Regime>();

    CancellationTokenSource regimeTokenSource = new CancellationTokenSource();
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
            if (!connected)
            {
                bool changed = statusRequestReceived;
                statusRequestReceived = false;
                if (changed) OnReady();
            }
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
        regimeTokenSource?.Cancel();
        Connection?.StopListening();
        io.Stop();
    }

    protected void OnReady()
    {
        Ready?.Invoke(this, IsReady);

        //start up message sequence
        executeRegimes();
    }

    public void AddRegime(Regime regime)
    {
        regimes.Add(regime);
    }

    private void executeRegimes()
    {
        if (regimes.Count == 0) return;

        Task.Run(() =>
        {
            foreach (var regime in regimes)
            {
                foreach (var regimeItem in regime.Items)
                {
                    for (int i = 0; i <= regime.RepeatCount; i++)
                    {
                        try
                        {
                            if (regimeItem.Message != null)
                            {
                                SendMessage(regimeItem.Message);
                            }
                            else if (regimeItem.Delay > 0)
                            {
                                Thread.Sleep(regimeItem.Delay);
                            }
                            else if (regime.DefaultDelay > 0)
                            {
                                Thread.Sleep(regime.DefaultDelay);
                            }
                        }
                        catch (Exception e)
                        {
                            ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(e));
                        }
                        if (regimeTokenSource.Token.IsCancellationRequested) break;
                    }
                }
                if (regimeTokenSource.Token.IsCancellationRequested) break;
            }
        }, regimeTokenSource.Token);
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
                    response.Add(255);
                    handled = true;
                    bool changed = !statusRequestReceived;
                    statusRequestReceived = true;
                    if (changed) OnReady();
                    break;

                case MessageType.PING:
                    response.Type = MessageType.PING_RESPONSE;
                    response.Add(DateTime.Now.Millisecond);
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
