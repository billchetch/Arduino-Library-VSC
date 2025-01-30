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

    #region Events
    public event EventHandler<bool>? Ready;
    public event EventHandler<ArduinoMessageMap.UpdatedProperties>? MessageReceived;
    #endregion

    #region Properties
    public byte ID { get; set; } = DEFAULT_BOARD_ID;

    public String Name {get; internal set; } = DEFAULT_NAME;

    public String UID => Name; //for IMessageUpdatable interface compliance

    public bool IsConnected => connection != null && connection.IsConnected;
    public bool IsReady => IsConnected && statusResponseReceived;

    [ArduinoMessageMap(MessageType.STATUS_RESPONSE, 0)]
    public int Millis { get; internal set; } = -1;

    //[ArduinoMessageMap(MessageType.STATUS_RESPONSE, 1)]
    public int FreeMemory { get; internal set; } = -1;
    #endregion
    
    #region Fields
    IConnection? connection;
   
    Frame inboundFrame;
    Frame outboundFrame;

    Object sendMessageLock = new object(); //make writing bytes to underlying connection thread-safe

    ArduinoMessage? lastMessageReceived;
    DateTime lastMessageReceivedOn;

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
                    OnMessageReceived(message);
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

                //Console.WriteLine("Error when handling frame: {0}", e.Message);
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
            Console.WriteLine("Connected: {0}", connected);

            //here should be something like: await RequestSTtaus
            if(connected)
            {
                Task<bool> t;
                statusResponseReceived = false;
                do
                {
                    Console.WriteLine("Connected so Requesting status,,,");
                    t = RequestStatus().OnReceivedAsync((response) =>{
                        if(response.Type == MessageType.STATUS_RESPONSE)
                        {
                            statusResponseReceived = true;
                        }
                    }, 2);
                    await t;
                } while(!statusResponseReceived);

                //ok so here we have a status resopnse
                Ready?.Invoke(this, IsReady);
            }
            else
            {
                statusResponseReceived = false;
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

        ArduinoRequest.Handle(message); //this trigger callbacks per request

        if(IsReady)
        {
            Console.WriteLine("Received {0} for {1} from {2}", message.Type, message.Target, message.Sender);
            
            ArduinoMessageMap.UpdatedProperties updatedProperties = new ArduinoMessageMap.UpdatedProperties();
            switch(message.Target){
                case ArduinoMessage.NO_TARGET:
                    //what to do?
                    break;

                default:
                    if(message.Target == ID)
                    {
                        updatedProperties = HandleMessage(message);
                    } 
                    else
                    {
                        var dev = getDevice(message.Target);
                        updatedProperties = dev.HandleMessage(message);
                    }
                    break;
            }
            MessageReceived?.Invoke(this, updatedProperties);
        }
    }

    public ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        return ArduinoMessageMap.AssignMessageValues(this, message);
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

    public ArduinoRequest RequestStatus(byte target = ArduinoMessage.NO_TARGET)
    {
        if(target == ArduinoMessage.NO_TARGET)
        {
            target = DEFAULT_BOARD_ID;
        }
        var msg = new ArduinoMessage(MessageType.STATUS_REQUEST);
        msg.Target = target;
        var req = ArduinoRequest.Create(msg);
        SendMessage(msg);
        return req;
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
