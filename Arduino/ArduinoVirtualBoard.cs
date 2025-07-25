using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using Chetch.Arduino;
using Chetch.Arduino.Connections;
using Chetch.Arduino.Devices;
using Chetch.Messaging;
using Chetch.Utilities;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using XmppDotNet.Xmpp.Jingle;

namespace Chetch.Arduino;

public class ArduinoVirtualBoard
{
    #region Constants and Static methods

    public static ArduinoMessage CreateExecuteRegimeMessage(String regimeName)
    {
        var message = new ArduinoMessage(MessageType.COMMAND);
        message.Target = ArduinoBoard.DEFAULT_BOARD_ID;
        message.Add(ArduinoBoard.BoardCommand.BEGIN_TEST);
        message.Add(regimeName);

        return message;
    }

    public static void BeginRegime(ArduinoBoard board, String regimeName)
    {
        var message = CreateExecuteRegimeMessage(regimeName);
        message.Target = board.ID;
        board.SendMessage(message);
    }
    #endregion

    #region Classes and Enums
    public class Regime
    {
        public enum RegimeEvent
        {
            EXECUTION_BEGUN = 1,
            EXECUTION_ENDED,
            EXECUTION_CANCELLED
        }

        public class RegimeItem
        {
            public MessageType MessageType { get; internal set; } = MessageType.NOT_SET;

            public int Delay { get; internal set; } = 0;

            public ArduinoDevice? Device { get; internal set; }

            public String? PropertyName { get; internal set; }

            public Object? PropertyValue { get; internal set; }

            public RegimeItem(ArduinoDevice device, MessageType messageType, String? propertyName = null, Object? propertyValue = null)
            {
                Device = device;
                MessageType = messageType;
                Delay = 0;
                PropertyName = propertyName;
                PropertyValue = propertyValue;
            }

            public RegimeItem(int delay)
            {
                Device = null;
                Delay = delay;
            }
        }

        public const int DEFAULT_DELAY = 100;

        public event ErrorEventHandler? ExceptionThrown;
        public event EventHandler<RegimeEvent>? ExecutionChanged;

        public String Name { get; internal set; }

        public int DefaultDelay { get; set; } = DEFAULT_DELAY;

        public int RepeatCount { get; set; } = 0;

        public bool StartOnReady { get; set; } = false;

        public List<RegimeItem> Items = new List<RegimeItem>();

        public Action<ArduinoMessage>? SendMessage { get; set; }

        Task? xTask;

        CancellationTokenSource ctTokenSource = new CancellationTokenSource();

        public Regime(String name, int defaultDelay = DEFAULT_DELAY, int repeatCount = 0)
        {
            Name = name;
            DefaultDelay = defaultDelay;
            RepeatCount = repeatCount;
        }

        public void AddMessage(ArduinoDevice device, MessageType messageType, String? propertyName = null, Object? propertyValue = null)
        {
            Items.Add(new RegimeItem(device, messageType, propertyName, propertyValue));
        }
        public void AddDelay(int delay)
        {
            Items.Add(new RegimeItem(delay));
        }
        public Task Execute(int delay = 0)
        {
            xTask = Task.Run(() =>
            {
                if (delay > 0) Thread.Sleep(delay);

                ExecutionChanged?.Invoke(this, RegimeEvent.EXECUTION_BEGUN);
                try
                {
                    for (int i = 0; i <= RepeatCount; i++)
                    {
                        foreach (var regimeItem in Items)
                        {
                            if (regimeItem.Device != null)
                            {
                                if (SendMessage != null && regimeItem.PropertyName != null)
                                {

                                    var propInfo = regimeItem.Device.GetType().GetProperty(regimeItem.PropertyName);
                                    propInfo.SetValue(regimeItem.Device, regimeItem.PropertyValue);

                                    var message = ArduinoMessageMap.CreateMessageFor(regimeItem.Device, regimeItem.MessageType);
                                    message.Target = regimeItem.Device.ID;
                                    message.Sender = regimeItem.Device.ID;

                                    SendMessage(message);
                                }
                            }
                            else if (regimeItem.Delay > 0)
                            {
                                Thread.Sleep(regimeItem.Delay);
                            }
                            else if (DefaultDelay > 0)
                            {
                                Thread.Sleep(DefaultDelay);
                            }
                            if (ctTokenSource.Token.IsCancellationRequested) break;
                        }
                        if (ctTokenSource.Token.IsCancellationRequested) break;
                    }
                }
                catch (Exception e)
                {
                    ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(e));
                }
                ExecutionChanged?.Invoke(this, RegimeEvent.EXECUTION_ENDED);
            }, ctTokenSource.Token);
            return xTask;
        }

        public Task Cancel()
        {
            ctTokenSource.Cancel();
            if (xTask != null)
            {
                ExecutionChanged?.Invoke(this, RegimeEvent.EXECUTION_CANCELLED);
                return xTask;
            }
            else
            {
                return Task.CompletedTask;
            }
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

    public String SID => String.Format("virtual-{0}", Board.SID);

    public bool IsListening => Connection != null && Connection.IsListening;

    public bool IsConnected => Connection != null && Connection.IsConnected;

    public bool IsReady => IsConnected && statusRequestReceived && statusResponseSent;

    public ArduinoBoard Board { get; internal set; }

    public IConnectionListener? Connection
    {
        get
        {
            return cnn;
        }
        set
        {
            if (value == null && cnn != null)
            {
                if (cnn.IsListening)
                {
                    throw new Exception("Connecdtion is still listening");
                }
                cnn = null;
            }
            else if (cnn != null)
            {
                throw new Exception("Cannot set Connection twice.  Set to null first");
            }
            else if (value != null)
            {
                cnn = value;
                cnn.Connected += (sender, connected) =>
                {
                    try
                    {
                        OnConnected(connected);
                    }
                    catch (Exception e)
                    {
                        ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(e));
                    }
                };
                cnn.DataReceived += (sender, data) =>
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
            }
        }
    }

    public String Name { get; set; } = "Virtual";

    public byte DeviceCount => (byte)Board.DeviceCount;
    #endregion

    #region Fields
    IConnectionListener? cnn;

    MessageIO<ArduinoMessage> io = new MessageIO<ArduinoMessage>(Frame.FrameSchema.SMALL_SIMPLE_CHECKSUM, MessageEncoding.SYSTEM_DEFINED);

    bool statusRequestReceived = false;
    bool statusResponseSent = false;

    List<Regime> regimes = new List<Regime>();

    CancellationTokenSource regimeTokenSource = new CancellationTokenSource();
    #endregion

    #region Constructors
    public ArduinoVirtualBoard(ArduinoBoard board)
    {
        ID = board.ID;
        Board = board;
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
                    if (response.Type == MessageType.STATUS_RESPONSE && statusRequestReceived && !statusResponseSent)
                    {
                        statusResponseSent = true;
                        OnReady();
                    }
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
                MessageSent?.Invoke(this, io.LastMessageDispatched);
            }
            catch (Exception e)
            {
                ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(e));
            }
        };
    }
    #endregion

    #region Lifecycle
    public void Begin()
    {
        if (Connection == null)
        {
            throw new Exception("Connecdtion is null");
        }
        io.Start();
        Connection.StartListening();
    }

    public async Task End()
    {
        foreach (var regime in regimes)
        {
            await regime.Cancel();
        }
        Connection?.StopListening();
        io.Stop();
    }

    protected void OnConnected(bool connected)
    {
        if (connected)
        {
            Thread.Sleep(2000);  //Pauses simulates Serial connection
        }
        else
        {
            bool changed = statusRequestReceived;
            statusRequestReceived = false;
            statusResponseSent = false;
            if (changed) OnReady();
        }
    }

    /// <summary>
    /// Called when board changes Ready state.  If board is Ready it means it's sent a status response for a status request.
    /// The board is not ready once the connection has eneded.
    /// </summary>
    protected void OnReady()
    {
        Ready?.Invoke(this, IsReady);

        if (IsReady)
        {
            foreach (var reg in regimes)
            {
                if (reg.StartOnReady)
                {
                    reg.Execute(2000);
                }
            }
        }
    }
    #endregion

    #region Test Regimes
    public void AddRegime(Regime regime)
    {
        regime.SendMessage = SendMessage;
        regime.ExceptionThrown += ExceptionThrown;
        regime.ExecutionChanged += (sender, regimeEvent) =>
        {
            var msg = new ArduinoMessage(MessageType.NOTIFICATION);
            msg.Target = ID;
            switch (regimeEvent)
            {
                case Regime.RegimeEvent.EXECUTION_BEGUN:
                    msg.Add(ArduinoBoard.NotificationEvent.TEST_BEGUN);
                    break;
                case Regime.RegimeEvent.EXECUTION_ENDED:
                    msg.Add(ArduinoBoard.NotificationEvent.TEST_ENDED);
                    break;

                case Regime.RegimeEvent.EXECUTION_CANCELLED:
                    msg.Add(ArduinoBoard.NotificationEvent.TEST_CANCELLED);
                    break;
            }
            msg.Add(regime.Name);
            SendMessage(msg);
        };
        regimes.Add(regime);
    }

    public Regime GetRegime(String regimeName)
    {
        if (regimeName == null)
        {
            throw new ArgumentNullException("Regime name cannot be null");
        }
        foreach (var reg in regimes)
        {
            if (reg.Name.Equals(regimeName, StringComparison.InvariantCultureIgnoreCase))
            {
                return reg;
            }
        }
        throw new Exception(String.Format("Cannot find regime with name {0}", regimeName));
    }

    //TODO: Add search options if required
    public List<Regime> GetRegimes()
    {
        return regimes;
    }

    public bool RegimeExists(String regime)
    {
        return GetRegime(regime) != null;
    }

    public void ExecuteRegime(String regimeName, int delay = 0)
    {
        var regime = GetRegime(regimeName);
        if (regime != null)
        {
            regime.Execute(delay);
        }
    }
    #endregion

    #region Messaging
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
                    statusRequestReceived = true;
                    break;

                case MessageType.PING:
                    response.Type = MessageType.PING_RESPONSE;
                    response.Add(DateTime.Now.Millisecond);
                    handled = true;
                    break;

                case MessageType.COMMAND:
                    response.Type = MessageType.COMMAND_RESPONSE;
                    var cmd = message.Get<ArduinoBoard.BoardCommand>(0);
                    switch (cmd)
                    {
                        case ArduinoBoard.BoardCommand.BEGIN_TEST:
                            String testName = message.Get<String>(1);
                            if (RegimeExists(testName))
                            {
                                ExecuteRegime(testName);
                            }
                            handled = true;
                            break;

                        default:
                            break;
                    }
                    break;
            }
            response.Target = message.Target;
        }
        else if (message.Target >= ArduinoBoard.START_DEVICE_IDS_AT)
        {
            //we will assume this is a device but use a virtual method to allow specific board overrides
            handled = HandleDeviceMessageReceived(message, response);
        }
        return handled;
    }

    virtual protected bool HandleDeviceMessageReceived(ArduinoMessage message, ArduinoMessage response)
    {
        bool handled = false;
        switch (message.Type)
        {
            case MessageType.STATUS_REQUEST:
                response.Type = MessageType.STATUS_RESPONSE;
                handled = HandleDeviceStatusRequest(message, response);
                break;

            case MessageType.COMMAND:
                response.Type = MessageType.COMMAND_RESPONSE;
                var command = message.Get<ArduinoDevice.DeviceCommand>(0);
                handled = HandleDeviceCommand(command, message, response);
                break;
        }
        response.Target = message.Target;
        response.Sender = message.Sender;
        return handled;
    }

    virtual protected bool HandleDeviceStatusRequest(ArduinoMessage message, ArduinoMessage response)
    {
        try
        {
            var device = Board.GetDevice(message.Target);
            var msg = ArduinoMessageMap.CreateMessageFor(device, MessageType.STATUS_RESPONSE);
            response.Arguments.AddRange(msg.Arguments);
        }
        catch (Exception e)
        {
            ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(e));
        }

        return true;
    }

    virtual protected bool HandleDeviceCommand(ArduinoDevice.DeviceCommand command, ArduinoMessage message, ArduinoMessage response)
    {
        try
        {
            var device = Board.GetDevice(message.Target);
            var msg = ArduinoMessageMap.CreateMessageFor(device, MessageType.COMMAND_RESPONSE);
            response.Arguments.AddRange(msg.Arguments);

            ArduinoMessage? delayedMessage = null;
            if (device is ActiveSwitch)
            {
                switch (command)
                {
                    case ArduinoDevice.DeviceCommand.ON:
                    case ArduinoDevice.DeviceCommand.OFF:
                        bool on = command == ArduinoDevice.DeviceCommand.ON;
                        var propInfo = device.GetType().GetProperty("PinState");
                        if (propInfo != null)
                        {
                            propInfo.SetValue(device, on);
                            delayedMessage = ArduinoMessageMap.CreateMessageFor(device, MessageType.DATA);
                        }
                        break;
                }
            }
            if (delayedMessage != null)
            {
                delayedMessage.Target = device.ID;
                delayedMessage.Sender = device.ID;
                SendDelayedMessage(delayedMessage);    
            }
        }
        catch (Exception e)
        {
            ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(e));
        }
        return true;
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

    public void SendDelayedMessage(ArduinoMessage message, int delay = 100)
    {
        Task.Run(() =>
        {
            Thread.Sleep(delay);
            try
            {
                SendMessage(message);
            }
            catch (Exception) { }
        });
    }
    #endregion
}
