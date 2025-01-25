using Chetch.Messaging;

namespace Chetch.Arduino;

public class ArduinoBoard
{

    #region Events
    public event EventHandler<bool>? Ready;
    public event EventHandler<ArduinoMessage>? MessageReceived;
    #endregion

    #region Properties
    public bool IsReady => connection != null && connection.IsConnected;
    #endregion
    
    #region Fields
    IConnection? connection;

    Frame inboundFrame;
    Frame outboundFrame;
    #endregion

    public ArduinoBoard(int productID, int baudRate, Frame.FrameSchema frameSchema = Frame.FrameSchema.SMALL_SIMPLE_CHECKSUM)
    {
        connection = new ArduinoSerialConnection(productID, baudRate);
        inboundFrame = new Frame(frameSchema);
        outboundFrame = new Frame(frameSchema, MessageEncoding.BYTES_ARRAY);
    }

    public void Begin()
    {
        if(connection == null)
        {
            throw new Exception("Cannot Begin as no connection has been supplied");
        }
        
        connection.Connected += (sender, connected) => {
            Console.WriteLine("Connected: {0}", connected);

            //here should be something like: await RequestSTtaus
            Ready?.Invoke(this, connected);
        };

        connection.DataReceived += (sender, data) => {
            Console.WriteLine("Received {0} bytes", data.Length);
            try
            {
                if(inboundFrame.Add(data))
                {
                    inboundFrame.Validate();
                    var message = ArduinoMessage.Deserialize(inboundFrame.Payload);
                    inboundFrame.Reset();
                    OnMessageReceived(message);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                inboundFrame.Reset();
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
        if(IsReady)
        {
            MessageReceived?.Invoke(this, message);
        }
    }

    public void SendMessage(ArduinoMessage msg)
    {
        if(!IsReady)
        {
            throw new Exception("Board is not ready");
        }

        outboundFrame.Reset();
        outboundFrame.Payload = (msg.Serialize());

        connection?.SendData(outboundFrame.GetBytes().ToArray());
    }
    #endregion
}
