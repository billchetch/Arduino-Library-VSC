using System.Reflection;
using Chetch.Messaging;
using Chetch.Arduino.Connections;
using Microsoft.Extensions.Logging;
using Pomelo.EntityFrameworkCore.MySql.Storage.Internal;
using XmppDotNet;

namespace Chetch.Arduino;

public class ArduinoBoard : IMessageUpdatableObject
{
    #region Constants
    public const byte DEFAULT_BOARD_ID = 1;

    public const String DEFAULT_SID = "Unknown";

    public const byte START_DEVICE_IDS_AT = 10;

    public const MessageEncoding DEFAULT_MESSAGE_ENCODING = MessageEncoding.BYTES_ARRAY;

    public const int MAX_FRAME_PAYLOAD_SIZE = 50;

    public const int REQUEST_STATUS_TIMER_INTERVAL = 15; //in secs
    #endregion

    #region Classes and Enums
    public class ErrorEventArgs
    {
        public ErrorCode Error = ErrorCode.NO_ERROR;
        public ArduinoMessage? ErrorMessage;
        public Object? ErrorSource;

        public ErrorEventArgs(ErrorCode error, ArduinoMessage message, Object source)
        {
            Error = error;
            ErrorMessage = message;
            ErrorSource = source;
        }
    }

    public class NotificationEventArgs
    {
        public NotificationEvent Event = NotificationEvent.UNKNOWN;

        public ArduinoMessage? Message;
        public Object? Source;

        public NotificationEventArgs(NotificationEvent eventID, ArduinoMessage message, Object source)
        {
            Event = eventID;
            Message = message;
            Source = source;
        }
    }

    public enum ErrorCode
    {
        NO_ERROR = 0,
        MESSAGE_FRAME_ERROR = 10, //To indicate this is a Frame error
        MESSAGE_ERROR = 20,
        TARGET_NOT_VALID = 29,
        TARGET_NOT_SUPPLIED = 30,
        TARGET_NOT_FOUND = 31,
        MESSAGE_TYPE_PROHIBITED = 32, //if a particular target rejects a message type
        NO_DEVICE_ID = 40,
        DEVICE_LIMIT_REACHED = 41,
        DEVICE_ID_ALREADY_USED = 42,
        DEVICE_NOT_FOUND = 43,
        DEVICE_ERROR = 100, //To indicate this is an error from the device (not Board)
    }

    public enum NotificationEvent
    {
        UNKNOWN = 0,
        TEST_BEGUN,
        TEST_ENDED,
        TEST_CANCELLED,
    }

    public enum BoardCommand
    {
        NONE = 0,
        BEGIN_TEST,
        END_TEST,
    }
    #endregion

    #region Events
    public event EventHandler<bool>? Ready;
    public event EventHandler<ArduinoMessage>? MessageReceived;
    public event EventHandler<ArduinoMessage>? MessageSent;
    public event EventHandler<ErrorEventArgs>? ErrorReceived;
    public event EventHandler<NotificationEventArgs>? NotificationReceived;
    public event ErrorEventHandler? ExceptionThrown;
    #endregion

    #region Properties
    [ArduinoMessageMap(MessageType.ERROR, 0)]
    public ErrorCode Error { get; set; } = ErrorCode.NO_ERROR;

    public byte ID { get; set; } = DEFAULT_BOARD_ID;

    public String SID { get; internal set; } = DEFAULT_SID;

    public String UID => SID; //for IMessageUpdatable interface compliance

    public IConnection? Connection
    {
        get
        {
            return cnn;
        }
        set
        {
            if (value == null && cnn != null)
            {
                if (cnn.IsConnected)
                {
                    throw new Exception("Connecdtion is still connected");
                }
                cnn = null;
            }
            else if (cnn != null)
            {
                throw new Exception("Cannot set Connection twice.  Set to null first");
            }
            else if(value != null)
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

    public bool IsConnected => Connection != null && Connection.IsConnected;
    public bool IsReady => IsConnected && statusResponseReceived;

    [ArduinoMessageMap(MessageType.STATUS_RESPONSE, 0)]
    public String Name { get; internal set; } = "Unknown";

    [ArduinoMessageMap(MessageType.STATUS_RESPONSE, 1)]
    public int Millis { get; internal set; } = -1;

    [ArduinoMessageMap(MessageType.STATUS_RESPONSE, 2)]
    public int DeviceCount { get; internal set; } = -1;

    [ArduinoMessageMap(MessageType.STATUS_RESPONSE, 3)]
    public int FreeMemory { get; internal set; } = -1;

    public String StatusSummary => IsReady ? String.Format("Board: {0}, Memory: {1}, Devices: {2}", Name, FreeMemory, DeviceCount) : "Not Ready";

    public String MessageSummary => IsReady && io.LastMessageReceived != null ? String.Format("Received: {0} {1}s ago", io.LastMessageReceived.Type, Math.Round((DateTime.Now - io.LastMessageReceived.Created).TotalSeconds, 1)) : "No messages received";
    #endregion

    #region Fields
    MessageIO<ArduinoMessage> io = new MessageIO<ArduinoMessage>(Frame.FrameSchema.SMALL_SIMPLE_CHECKSUM, MessageEncoding.SYSTEM_DEFINED); 

    System.Timers.Timer requestStatusTimer = new System.Timers.Timer();

    IConnection? cnn;

    bool statusRequested = false;
    bool statusResponseReceived = false;

    Dictionary<byte, ArduinoDevice> devices = new Dictionary<byte, ArduinoDevice>();
    #endregion

    #region Constructors
    public ArduinoBoard(byte id, String sid)
    {
        ID = id;
        SID = sid;

        //Configure request status timer, this effectively pings the board if no message has been
        //received for some period .. starts based on board being ready or not (see OnReady)
        requestStatusTimer.AutoReset = true;
        requestStatusTimer.Interval = REQUEST_STATUS_TIMER_INTERVAL * 1000;
        requestStatusTimer.Elapsed += (sender, eargs) =>
        {
            if (IsReady && io.LastMessageReceived.Created != default && (DateTime.Now - io.LastMessageReceived.Created).TotalSeconds > REQUEST_STATUS_TIMER_INTERVAL)
            {
                try
                {
                    RequestStatus();
                }
                catch { }
                ;
            }
        };

        //Configure message IO
        io.ExceptionThrown += ExceptionThrown;
        io.MessageReceived += (sender, message) =>
        {
            if (IsReady || (message.Type == MessageType.STATUS_RESPONSE && message.Target == ID && statusRequested) || message.Type == MessageType.ERROR)
            {
                try
                {
                    //this will route the message
                    if (!OnMessageReceived(message))
                    {
                        //TODO: if here means the message has not been processed
                    } 
                }
                catch (Exception e)
                {
                    ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(e));
                }
            }
            else
            {
                var e = new Exception(String.Format("Board is not yet Ready to process messages of type {0} from {1}", message.Type, message.Sender));
                ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(e));
            }
        };
        io.MessageDispatched += (sender, bytes) =>
        {
            try
            {
                Connection?.SendData(bytes);
                MessageSent?.Invoke(this, io.LastMessageDispatched);
            }
            catch (Exception e)
            {
                ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(e));
            }
        };
    }

    public ArduinoBoard(String sid) : this(DEFAULT_BOARD_ID, sid)
    { }
    #endregion

    #region Lifecycle
    virtual public void Begin()
    {
        if (Connection == null)
        {
            throw new Exception("Cannot Begin as no connection has been supplied");
        }
   
        //Start message IO and Connect
        io.Start();
        Connection.Connect();
    }

    virtual public void End()
    {
        io.Stop();
        Connection?.Disconnect();
    }

    /// <summary>
    /// When a connection is made then run a task to request status as the response is crucial for IsReady property
    /// When a connecton is ended update stuff so IsReady = false
    /// </summary>
    /// <param name="connected"></param>
    /// <exception cref="Exception"></exception>
    virtual protected void OnConnected(bool connected)
    {
        if (Connection == null)
        {
            throw new Exception("No connection object!");
        }

        if (connected)
        {
            try
            {
                Task.Run(() =>
                {
                    Thread.Sleep(2000); //allow a bit of time for the board to fire up
                    int attempts = 0;
                    do
                    {
                        //Console.WriteLine("Requesting status...");
                        try
                        {
                            RequestStatus();
                        }
                        catch { }
                        Thread.Sleep(250);
                    } while (IsConnected && !IsReady && ++attempts < 3);

                    if (!IsReady && IsConnected)
                    {
                        //try again
                        Connection.Reconnect();
                    }
                });
            }
            catch (Exception e)
            {
                ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(e));
            }
        }
        else
        {
            bool changed = statusResponseReceived;
            statusResponseReceived = false;
            statusRequested = false;
            if (changed) OnReady();
        }
    }

    virtual protected void OnReady()
    {
        Ready?.Invoke(this, IsReady);
        if (IsReady)
        {
            requestStatusTimer.Start();
        }
        else
        {
            requestStatusTimer.Stop();
        }
    }
    #endregion

    #region Messaging
    public bool AssignMessageValue(PropertyInfo propertyInfo, Object propertyValue, ArduinoMessage message)
    {
        propertyInfo.SetValue(this, propertyValue);
        return true;
    }


    /// <summary>
    /// Handles message routing
    /// </summary>
    /// <param name="message">The messaget to be routed.</param>
    /// <returns>bool</returns>
    protected bool OnMessageReceived(ArduinoMessage message)
    {
        bool handled = false;
        ArduinoMessageMap.UpdatedProperties updatedProperties = new ArduinoMessageMap.UpdatedProperties();
        switch (message.Target)
        {
            case ArduinoMessage.NO_TARGET:
                //what to do?
                handled = false;
                break;

            default:
                if (message.Target == ID)
                {
                    //Direct messages for this board
                    updatedProperties = HandleMessage(message);
                    handled = true;
                }
                else if (HasDevice(message.Target))
                {
                    //Direct message to device
                    var dev = GetDevice(message.Target);
                    if (dev.IsReady || (message.Type == MessageType.STATUS_RESPONSE && dev.StatusRequested) || (message.Type == MessageType.ERROR))
                    {
                        updatedProperties = dev.HandleMessage(message);
                        if (message.Type == MessageType.ERROR)
                        {
                            Error = ErrorCode.DEVICE_ERROR;
                            ErrorReceived?.Invoke(this, new ErrorEventArgs(Error, message, dev));
                        }
                        handled = true;
                    }
                }
                else
                {
                    //do nothing if no device
                    handled = false;
                }
                break;
        }

        if (IsReady && handled)
        {
            MessageReceived?.Invoke(this, message);
        }
        return handled;
    }

    /// <summary>
    /// Processes the message for this board, other targets (devices) will have their own version
    /// </summary>
    /// <param name="message">The messaget to be routed.</param>
    /// <returns>A map of properties updated by the message according to code attriutes (see ArduinoMessageMap)</returns>
    public ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        switch (message.Type)
        {
            case MessageType.STATUS_RESPONSE:
                bool readyChange = !statusResponseReceived;
                statusResponseReceived = true;

                //We update here so that any triggers resulting from property assignments will occur with IsReady = true
                var updatedProperties = ArduinoMessageMap.AssignMessageValues(this, message);
                if (readyChange)
                {
                    OnReady();
                }
                return updatedProperties;

            case MessageType.ERROR:
                updatedProperties = ArduinoMessageMap.AssignMessageValues(this, message);
                ErrorReceived?.Invoke(this, new ErrorEventArgs(Error, message, this));    
                return updatedProperties;

            case MessageType.NOTIFICATION:
                updatedProperties = ArduinoMessageMap.AssignMessageValues(this, message);
                var notificationEvent = message.Get<NotificationEvent>(0);
                if (HandleNotification(notificationEvent, message))
                {
                    NotificationReceived?.Invoke(this, new NotificationEventArgs(notificationEvent, message, this));
                }
                return updatedProperties;

            default:
                return ArduinoMessageMap.AssignMessageValues(this, message);
        }
    }

    virtual public bool HandleNotification(NotificationEvent notificationEvent, ArduinoMessage message)
    {
        //A hook
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

    public void RequestStatus(byte target = ArduinoMessage.NO_TARGET)
    {
        if (target == ArduinoMessage.NO_TARGET)
        {
            target = ID;
        }
        var msg = new ArduinoMessage(MessageType.STATUS_REQUEST);
        msg.Target = target;
        SendMessage(msg);
        statusRequested = true; //flag that this has been requested
    }

    public void Ping(byte target = ArduinoMessage.NO_TARGET)
    {
        if (target == ArduinoMessage.NO_TARGET)
        {
            target = ID;
        }
        var msg = new ArduinoMessage(MessageType.PING);
        msg.Target = target;
        SendMessage(msg);
    }
    #endregion

    #region Device management
    public void AddDevice(ArduinoDevice device)
    {
        if (device.ID > 0 && device.ID < START_DEVICE_IDS_AT)
        {
            throw new Exception(String.Format("Device ID {0} for device {1} is not allowed", device.ID, device.SID));
        }

        foreach (var dev in devices.Values)
        {
            if (dev.SID.Equals(device.SID))
            {
                throw new Exception(String.Format("SID {0} is already being used", device.SID));
            }
            if (dev.ID == device.ID)
            {
                throw new Exception(String.Format("ID {0} is already being used", device.ID));
            }
        }
        if (device.ID == 0)
        {
            device.ID = (byte)(devices.Count + START_DEVICE_IDS_AT);
        }

        devices[device.ID] = device;
        device.Board = this;
    }

    public void AddDevices(ICollection<ArduinoDevice> devices)
    {
        foreach (var device in devices)
        {
            AddDevice(device);
        }
    }

    public ArduinoDevice GetDevice(byte id)
    {
        if (id < START_DEVICE_IDS_AT)
        {
            throw new Exception(String.Format("{0} is nNot a valid device ID", id));
        }
        if (!devices.ContainsKey(id))
        {
            throw new Exception(String.Format("Device with ID {0} not found", id));
        }
        return devices[id];
    }

    public bool HasDevice(byte id)
    {
        return devices.ContainsKey(id);
    }

    public ArduinoDevice? GetDevice(String sid)
    {
        if (sid == null)
        {
            throw new ArgumentException("SID cannot be null");
        }

        foreach (var device in devices.Values)
        {
            if (device.SID != null && sid.Equals(device.SID, StringComparison.InvariantCultureIgnoreCase))
            {
                return device;
            }
        }
        return null;
    }
    #endregion
}
