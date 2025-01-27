using Chetch.Messaging;

namespace Chetch.Arduino;

public class ArduinoBoard
{

    #region Constants
    public const byte DEFAULT_BOARD_ID = 1;
    #endregion

    #region Events
    public event EventHandler<bool>? Ready;
    public event EventHandler<ArduinoMessage>? MessageReceived;
    #endregion

    #region Properties
    public byte ID { get; } = DEFAULT_BOARD_ID;
    public bool IsConnected => connection != null && connection.IsConnected;
    public bool IsReady => IsConnected && statusResponseReceived;
    #endregion
    
    #region Fields
    IConnection? connection;

    bool statusResponseReceived = false;

    Frame inboundFrame;
    Frame outboundFrame;
    #endregion

    public ArduinoBoard(int productID, int baudRate, Frame.FrameSchema frameSchema = Frame.FrameSchema.SMALL_SIMPLE_CHECKSUM)
    {
        connection = new ArduinoSerialConnection(productID, baudRate);
        inboundFrame = new Frame(frameSchema);
        inboundFrame.FrameComplete += (frame, complete) => {
            try
            {
                var message = ArduinoMessage.Deserialize(inboundFrame.Payload);
                inboundFrame.Reset();
                OnMessageReceived(message);
            } 
            catch (Exception e)
            {
                Console.WriteLine("Error when handling frame: {0}", e.Message);
            }
        };
        outboundFrame = new Frame(frameSchema, MessageEncoding.BYTES_ARRAY);
    }

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

    #region Messaging
    protected void OnMessageReceived(ArduinoMessage message)
    {
        ArduinoRequest.Handle(message); //this trigger callbacks per request

        if(IsReady)
        {
            Console.WriteLine("Received {0} for {1} from {2}", message.Type, message.Target, message.Sender);
            switch(message.Target){
                case ArduinoMessage.NO_TARGET:
                    //what to do?
                    break;

                default:
                    if(message.Target == ID)
                    {

                    } 
                    else
                    {
                        
                    }
                    break;
            }
            MessageReceived?.Invoke(this, message);
        }
    }

    public void SendMessage(ArduinoMessage msg)
    {
        if(!IsConnected)
        {
            throw new Exception("Board is not connected");
        }

        if(msg.Sender == ArduinoMessage.NO_SENDER)
        {
            msg.Sender = ID; //this is the default
        }
        
        outboundFrame.Reset();
        outboundFrame.Payload = msg.Serialize();
        
        connection?.SendData(outboundFrame.GetBytes().ToArray());
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
}
