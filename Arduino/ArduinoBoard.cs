using Chetch.Messaging;
using Pomelo.EntityFrameworkCore.MySql.Storage.Internal;
using XmppDotNet;

namespace Chetch.Arduino;

public class ArduinoBoard : IMessageUpdatableObject
{

    #region Constants
    public const byte DEFAULT_BOARD_ID = 1;

    public const String DEFAULT_NAME = "Unknown";

    public const byte START_DEVICE_IDS_AT = 10; 

    public const MessageEncoding DEFAULT_MESSAGE_ENCODING = MessageEncoding.BYTES_ARRAY;

    public const int MAX_FRAME_PAYLOAD_SIZE = 50;
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
    public event EventHandler<ArduinoMessageMap.UpdatedProperties>? MessageReceived;
    public event EventHandler<ErrorEventArgs>? ErrorReceived;
    #endregion

    #region Properties
    [ArduinoMessageMap(MessageType.ERROR, 0)]
    public ErrorCode Error {get; set; } = ErrorCode.NO_ERROR;

    public byte ID { get; set; } = DEFAULT_BOARD_ID;

    public String Name {get; internal set; } = DEFAULT_NAME;

    public String UID => Name; //for IMessageUpdatable interface compliance

    public bool IsConnected => connection != null && connection.IsConnected;
    public bool IsReady => IsConnected && statusResponseReceived;

    [ArduinoMessageMap(MessageType.STATUS_RESPONSE, 0)]
    public int Millis { get; internal set; } = -1;

     [ArduinoMessageMap(MessageType.STATUS_RESPONSE, 1)]
    public int DeviceCount { get; internal set; } = -1;

    [ArduinoMessageMap(MessageType.STATUS_RESPONSE, 2)]
    public int FreeMemory { get; internal set; } = -1;
    #endregion
    
    #region Fields
    IConnection? connection;
   
    Frame inboundFrame;
    Frame outboundFrame;

    Object sendMessageLock = new object(); //make writing bytes to underlying connection thread-safe

    ArduinoMessage? lastMessageReceived;
    DateTime lastMessageReceivedOn;

    bool statusRequested = false;
    bool statusResponseReceived = false;

    Dictionary<byte, ArduinoDevice> devices = new Dictionary<byte, ArduinoDevice>();
    #endregion

    #region Constructors
    public ArduinoBoard(String name, int productID, int baudRate, Frame.FrameSchema frameSchema = Frame.FrameSchema.SMALL_SIMPLE_CHECKSUM)
    {
        Name = name;
        connection = new ArduinoSerialConnection(productID, baudRate);
        inboundFrame = new Frame(frameSchema, DEFAULT_MESSAGE_ENCODING, MAX_FRAME_PAYLOAD_SIZE);
        inboundFrame.FrameComplete +=  async (frame, payload) => {
            Task t = Task.Run(() => {
                try{
                    var message = ArduinoMessage.Deserialize(payload);

                    //we check that this message can indeed be processed by this board
                    if(IsReady || (message.Type == MessageType.STATUS_RESPONSE && message.Target == ID && statusRequested))
                    {
                        OnMessageReceived(message);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Deserialising and processing message resulted in an exception: {0}", e.Message);
                }
            });

            try
            {
                await t;
            }
            catch (ArgumentException ae)
            {

                Console.WriteLine("Error when handling frame: {0}", ae.Message);
            }
        };
        outboundFrame = new Frame(frameSchema, inboundFrame.Encoding, inboundFrame.MaxPayload);
    }
    #endregion

    #region Lifecycle
    public void Begin(bool allowEmptyBoard = false)
    {
        if(connection == null)
        {
            throw new Exception("Cannot Begin as no connection has been supplied");
        }
        if(devices.Count == 0 && !allowEmptyBoard)
        {
            throw new Exception("Cannot begin as there are no devices for this board");
        }

        connection.Connected += async (sender, connected) => {
            //here should be something like: await RequestSTtaus
            if(connected)
            {
                await Task.Run(()=>{
                    do
                    {
                        Console.WriteLine("Requesting status...");
                        RequestStatus();
                        Thread.Sleep(1000);
                    } while(!IsReady);
                });
            }
            else
            {
                statusResponseReceived = false;
                statusRequested = false;
                Ready?.Invoke(this, IsReady);
            }
            
        };

        connection.DataReceived += (sender, data) => {
            //Console.WriteLine("Received {0} bytes", data.Length);
            foreach(var b in data){
                try{
                    inboundFrame.Add(b);    
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                    inboundFrame.Reset();
                }
            }
        };

        connection.Connect();
    }

    public void End(){
        connection?.Disconnect();
    }
    #endregion

    #region Messaging
    protected void OnMessageReceived(ArduinoMessage message)
    {
        lastMessageReceived = message;
        lastMessageReceivedOn = DateTime.Now;

        bool handled = false;
        ArduinoMessageMap.UpdatedProperties updatedProperties = new ArduinoMessageMap.UpdatedProperties();
        switch(message.Target){
            case ArduinoMessage.NO_TARGET:
                //what to do?
                handled = false;
                break;

            default:
                if(message.Target == ID)
                {
                    updatedProperties = HandleMessage(message);
                    if(message.Type == MessageType.ERROR)
                    {
                        ErrorReceived?.Invoke(this, new ErrorEventArgs(Error, message, this));
                    }
                    handled = true;
                } 
                else if(HasDevice(message.Target))
                {
                    var dev = getDevice(message.Target);
                    updatedProperties = dev.HandleMessage(message);
                    if(message.Type == MessageType.ERROR)
                    {
                        Error = ErrorCode.DEVICE_ERROR;
                        ErrorReceived?.Invoke(this, new ErrorEventArgs(Error, message, dev));
                    }
                    handled = true;
                } 
                else
                {
                    //do nothing if no device
                    handled = false;
                }
                break;
        }

        if(IsReady && handled)
        {
            MessageReceived?.Invoke(this, updatedProperties);        
        }
    }

    public ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        var updatedProperties = ArduinoMessageMap.AssignMessageValues(this, message);
        switch(message.Type)
        {
            case MessageType.STATUS_RESPONSE:
                statusResponseReceived = true;
                Ready?.Invoke(this, IsReady);
                break;
        }
        return updatedProperties;
    }

    public void SendMessage(ArduinoMessage message)
    {
        if(!IsConnected)
        {
            throw new Exception("Board is not connected");
        }

        if(message.Sender == ArduinoMessage.NO_SENDER)
        {
            message.Sender = ID; //this is the default
        }
        
        lock(sendMessageLock)
        {
            outboundFrame.Reset();
            outboundFrame.Payload = message.Serialize();
            
            connection?.SendData(outboundFrame.GetBytes().ToArray());
        }
    }

    public void RequestStatus(byte target = ArduinoMessage.NO_TARGET)
    {
        if(target == ArduinoMessage.NO_TARGET)
        {
            target = DEFAULT_BOARD_ID;
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
            throw new Exception(String.Format("Device ID {0} for device {1} is not allowed", device.ID, device.Name));
        }

        foreach(var dev in devices.Values)
        {
            if(dev.Name.Equals(device.Name))
            {
                throw new Exception(String.Format("Name {0} is already being used", device.Name));
            }
            if(dev.ID == device.ID)
            {
                throw new Exception(String.Format("ID {0} is already being used", device.ID));
            }
        }
        devices[device.ID] = device;
        device.Board = this;
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
