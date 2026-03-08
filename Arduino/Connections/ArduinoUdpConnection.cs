using System;
using System.Net.Sockets;
using System.Net;


namespace Chetch.Arduino.Connections;

public class ArduinoUdpConnection : IConnection
{
    #region Constants
    const int REMOTE_PORT = 18800;
    const int LOCAL_PORT = 42000; //for stability we fix the port

    #endregion

    #region Properties
    public String IP { get; set; } = String.Empty;

    public int Port { get; set; } = REMOTE_PORT;

    public bool IsConnected => client == null ? false : connected;
    #endregion

    #region Events
    public event EventHandler<byte[]> DataReceived;
    public event EventHandler<bool> Connected;
    #endregion

    #region Fields
    UdpClient? client;
    Task? readTask; 

    CancellationTokenSource ctkSource = new CancellationTokenSource();

    bool connected = false;
    #endregion

    #region Constructors
    public ArduinoUdpConnection(String ipAddress, int port = REMOTE_PORT)
    {
        IP = ipAddress;
        Port = port;
    }
    #endregion

    #region Methods
    public void Connect()
    {
        IPEndPoint localEP = new IPEndPoint(IPAddress.Any, LOCAL_PORT);
        if(client == null)
        {
            client = new UdpClient(localEP);
        }
        ctkSource = new CancellationTokenSource();
        client.Connect(IP, Port);
        connected = true;
        readTask = Task.Run(async () => {
            try
            {
                do
                {
                    UdpReceiveResult result = await client.ReceiveAsync(ctkSource.Token);
                    if(result.Buffer.Length > 0)
                    {
                        //Console.WriteLine("Received {0} bytes!", result.Buffer.Length);
                        DataReceived?.Invoke(this, result.Buffer);
                    }
                } while(!ctkSource.Token.IsCancellationRequested);
            } catch {}

        }, ctkSource.Token);

        Connected?.Invoke(this, IsConnected);
    }

    public void Disconnect()
    {
        var task = Task.Run(async () =>
        {
            if(readTask != null){
                ctkSource.Cancel();
                await readTask;
            }
            connected = false;
        });

        for(int i = 0; i < 10; i++)
        {
            if(task.IsCompleted)break;
            Thread.Sleep(10);
        }

        if(client != null)
        {
            client.Close();
            client.Dispose();
            client = null;
        }
        
        Connected?.Invoke(this, IsConnected);
    }

    public void Reconnect()
    {
        Disconnect();
        Thread.Sleep(100);
        Connect();
    }

    public void SendData(byte[] data)
    {
        if (!IsConnected)
        {
            throw new Exception("Cannot send data as not connected!");
        }
        if(client != null)
        {
            client.Send(data, data.Length);
        }
    }
    #endregion
}
