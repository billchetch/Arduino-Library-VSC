using System.Reflection;
using Chetch.Messaging;
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
    #endregion

    #region Events
    public event EventHandler<bool>? Ready;
    public event EventHandler<ArduinoMessage>? MessageReceived;
    public event EventHandler<ArduinoMessage>? MessageSent;
    public event EventHandler<ErrorEventArgs>? ErrorReceived;
    public event ErrorEventHandler? ExceptionThrown;
    #endregion

    #region Properties
    [ArduinoMessageMap(MessageType.ERROR, 0)]
    public ErrorCode Error {get; set; } = ErrorCode.NO_ERROR;

    public byte ID { get; set; } = DEFAULT_BOARD_ID;

    public String SID {get; internal set; } = DEFAULT_SID;

    public String UID => SID; //for IMessageUpdatable interface compliance

    public IConnection? Connection { get; set; }
   
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

    public String MessageSummary => IsReady && lastMessageReceived != null ? String.Format("Received: {0} {1}s ago", lastMessageReceived.Type, Math.Round((DateTime.Now - lastMessageReceivedOn).TotalSeconds, 1)) : "No messages received";
    #endregion
    
    #region Fields
    Frame inboundFrame;
    Frame outboundFrame;

    Object sendMessageLock = new object(); //make writing bytes to underlying connection thread-safe

    ArduinoMessage? lastMessageReceived;
    DateTime lastMessageReceivedOn;

    System.Timers.Timer requestStatusTimer = new System.Timers.Timer();

    bool statusRequested = false;
    bool statusResponseReceived = false;

    Dictionary<byte, ArduinoDevice> devices = new Dictionary<byte, ArduinoDevice>();
    #endregion

    #region Constructors
    public ArduinoBoard(byte id, String sid, Frame.FrameSchema frameSchema = Frame.FrameSchema.SMALL_SIMPLE_CHECKSUM)
    {
        ID = id;
        SID = sid;
        inboundFrame = new Frame(frameSchema, DEFAULT_MESSAGE_ENCODING, MAX_FRAME_PAYLOAD_SIZE);
        inboundFrame.FrameComplete +=  async (frame, payload) => {
            Task t = Task.Run(() => {
                try{
                    var message = ArduinoMessage.Deserialize(payload);

                    //we check that this message can indeed be processed by this board
                    if(IsReady || (message.Type == MessageType.STATUS_RESPONSE && message.Target == ID && statusRequested) || message.Type == MessageType.ERROR)
                    {
                        OnMessageReceived(message);
                    }
                }
                catch (Exception e)
                {
                    ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(e));
                }
            });

            try
            {
                await t;
            }
            catch (ArgumentException ae)
            {
                ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(ae));
            }
        };
        outboundFrame = new Frame(frameSchema, inboundFrame.Encoding, inboundFrame.MaxPayload);
    }

    public ArduinoBoard(String sid, Frame.FrameSchema frameSchema = Frame.FrameSchema.SMALL_SIMPLE_CHECKSUM) : this(DEFAULT_BOARD_ID, sid, frameSchema)
    {}
    #endregion

    #region Lifecycle
    public void Begin()
    {
        if(Connection == null)
        {
            throw new Exception("Cannot Begin as no connection has been supplied");
        }
        
        Connection.Connected += async (sender, connected) => {
            //here should be something like: await RequestSTtaus
            if(connected)
            {
                try
                {
                    await Task.Run(()=>{
                        Thread.Sleep(2000); //allow a bit of time for the board to fire up
                        do
                        {
                            //Console.WriteLine("Requesting status...");
                            RequestStatus();
                            Thread.Sleep(1000);
                        } while (!IsReady);
                    });
                }
                catch(Exception e)
                {
                    ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(e));
                }
            }
            else
            {
                bool changed = statusResponseReceived;
                statusResponseReceived = false;
                statusRequested = false;
                if(changed)OnReady();
            }
            
        };

        Connection.DataReceived += (sender, data) => {
            foreach(var b in data){
                try{
                    inboundFrame.Add(b);    
                }
                catch(Exception e)
                {
                    inboundFrame.Reset();
                    ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(e));
                }
            }
        };

        requestStatusTimer.AutoReset = true;
        requestStatusTimer.Interval = REQUEST_STATUS_TIMER_INTERVAL * 1000;
        requestStatusTimer.Elapsed += (sender, eargs) => {
            if(IsReady && lastMessageReceivedOn != default(DateTime) && (DateTime.Now - lastMessageReceivedOn).TotalSeconds > REQUEST_STATUS_TIMER_INTERVAL)
            {
                try
                {
                    RequestStatus();
                } catch {};
            }
        };

        Connection.Connect();
    }

    public void End(){
        Connection?.Disconnect();
    }
    
    protected void OnReady()
    {
        Ready?.Invoke(this, IsReady);
        if(IsReady)
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
    public bool CanAssignMessageValue(PropertyInfo propertyInfo, ArduinoMessage message)
    {
        return true;
    }
    
    protected void OnMessageReceived(ArduinoMessage message)
    {
        lastMessageReceived = message;
        lastMessageReceivedOn = DateTime.Now;

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
                    updatedProperties = HandleMessage(message);
                    if (message.Type == MessageType.ERROR)
                    {
                        ErrorReceived?.Invoke(this, new ErrorEventArgs(Error, message, this));
                    }
                    handled = true;
                }
                else if (HasDevice(message.Target))
                {
                    var dev = getDevice(message.Target);
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
    }

    public ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        switch(message.Type)
        {
            case MessageType.STATUS_RESPONSE:
                bool readyChange = !statusResponseReceived;
                statusResponseReceived = true;

                //We update here so that any triggers resulting from property assignments will occur with IsReady = true
                var updatedProperties = ArduinoMessageMap.AssignMessageValues(this, message);
                if(readyChange)
                {
                    OnReady();
                }
                return updatedProperties;

            default:
                return ArduinoMessageMap.AssignMessageValues(this, message);
        }
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

        lock (sendMessageLock)
        {
            outboundFrame.Reset();
            outboundFrame.Payload = message.Serialize();

            Connection?.SendData(outboundFrame.GetBytes().ToArray());
        }

        MessageSent?.Invoke(this, message);
    }

    public void RequestStatus(byte target = ArduinoMessage.NO_TARGET)
    {
        if(target == ArduinoMessage.NO_TARGET)
        {
            target = ID;
        }
        var msg = new ArduinoMessage(MessageType.STATUS_REQUEST);
        msg.Target = target;
        SendMessage(msg);
        statusRequested = true; //flag that this has been requested
    }
    #endregion

    #region Device management
    public void AddDevice(ArduinoDevice device)
    {
        if(device.ID < START_DEVICE_IDS_AT)
        {
            throw new Exception(String.Format("Device ID {0} for device {1} is not allowed", device.ID, device.SID));
        }

        foreach(var dev in devices.Values)
        {
            if(dev.SID.Equals(device.SID))
            {
                throw new Exception(String.Format("SID {0} is already being used", device.SID));
            }
            if(dev.ID == device.ID)
            {
                throw new Exception(String.Format("ID {0} is already being used", device.ID));
            }
        }
        devices[device.ID] = device;
        device.Board = this;
    }

    public void AddDevices(ICollection<ArduinoDevice> devices)
    {
        foreach(var device in devices)
        {
            AddDevice(device);
        }
    }
    
    public ArduinoDevice getDevice(byte id)
    {
        if(id < START_DEVICE_IDS_AT)
        {
            throw new Exception(String.Format("{0} is nNot a valid device ID", id));
        }
        if(!devices.ContainsKey(id))
        {
            throw new Exception(String.Format("Device with ID {0} not found", id));
        }
        return devices[id];
    }

    public bool HasDevice(byte id)
    {
        return devices.ContainsKey(id);
    }
    #endregion
}
