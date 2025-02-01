using Chetch.Messaging;
using Pomelo.EntityFrameworkCore.MySql.Storage.Internal;

namespace Chetch.Arduino;

public class ArduinoBoard : IMessageUpdatableObject
{

    #region Constants
    public const byte DEFAULT_BOARD_ID = 1;

    public const String DEFAULT_NAME = "Unknown";

    public const byte START_DEVICE_IDS_AT = 10; 
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

    List<ArduinoDevice> devices = new List<ArduinoDevice>();
    #endregion

    #region Constructors
    public ArduinoBoard(String name, int productID, int baudRate, Frame.FrameSchema frameSchema = Frame.FrameSchema.SMALL_SIMPLE_CHECKSUM)
    {
        Name = name;
        connection = new ArduinoSerialConnection(productID, baudRate);
        inboundFrame = new Frame(frameSchema);
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
        outboundFrame = new Frame(frameSchema, MessageEncoding.BYTES_ARRAY);
    }
    #endregion

    #region Lifecycle
    public void Begin()
    {
        if(connection == null)
        {
            throw new Exception("Cannot Begin as no connection has been supplied");
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

                Ready?.Invoke(this, IsReady);
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
                    //Console.WriteLine(e.Message);
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

        ArduinoMessageMap.UpdatedProperties updatedProperties = new ArduinoMessageMap.UpdatedProperties();
        switch(message.Target){
            case ArduinoMessage.NO_TARGET:
                //what to do?
                break;

            default:
                if(message.Target == ID)
                {
                    updatedProperties = HandleMessage(message);
                    if(message.Type == MessageType.ERROR)
                    {
                        ErrorReceived?.Invoke(this, new ErrorEventArgs(Error, message, this));
                    }
                } 
                else
                {
                    var dev = getDevice(message.Target);
                    updatedProperties = dev.HandleMessage(message);
                    if(message.Type == MessageType.ERROR)
                    {
                        Error = ErrorCode.DEVICE_ERROR;
                        ErrorReceived?.Invoke(this, new ErrorEventArgs(Error, message, dev));
                    }
                }
                break;
        }

        if(IsReady)
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
    public void AddDevice(ArduinoDevice device){
        byte id = (byte)(devices.Count + START_DEVICE_IDS_AT);
        foreach(var dev in devices)
        {
            if(dev.Name.Equals(device.Name))
            {
                throw new Exception(String.Format("Name {0} is already being used", device.Name));
            }
        }
        devices.Add(device);
        device.ID = id;
        device.Board = this;
    }

    public ArduinoDevice getDevice(byte id){
        if(id < START_DEVICE_IDS_AT || id >= devices.Count + START_DEVICE_IDS_AT)
        {
            throw new Exception(String.Format("{0} is nNot a valid device ID", id));
        }
        var dev = devices[id - START_DEVICE_IDS_AT];
        if(dev.ID != id)
        {
            throw new Exception("Device IDs out of sync");
        }
        return dev;
    }
    #endregion
}
