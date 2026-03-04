using System;
using System.Net.Sockets;
using System.Text;

namespace Chetch.Arduino.Connections;

public class ArduinoTcpConnection : IConnection
{
    public bool IsConnected => client == null ? false : client.Connected;

    public String IPAddress { get; set; } = String.Empty;

    public int Port { get; set; } = 80;

    public event EventHandler<byte[]> DataReceived;
    public event EventHandler<bool> Connected;


    #region Fields
    TcpClient? client;
    Task? readTask; 

    CancellationTokenSource ctkSource = new CancellationTokenSource();

    #endregion
    public void Connect()
    {
        //Console.WriteLine("Connecting to: {0}:{1}", IPAddress, Port);
        if(client == null)
        {
            client = new TcpClient();
        }
        client.Connect(IPAddress, Port);
        client.NoDelay = true;
        ctkSource = new CancellationTokenSource();

        readTask = Task.Run(async ()=>{
            //Console.WriteLine("Firing up read task");
            byte[] buffer = new byte[1024];
            var stream = client.GetStream();
            do
            {
                try
                {
                    int k  = await stream.ReadAsync(buffer, 0, buffer.Length, ctkSource.Token);
                    if(k > 0)
                    {
                        byte[] data = new byte[k];
                        Array.Copy(buffer, data, k);
                        DataReceived?.Invoke(this, data);
                    }
                } catch {}
            }
            while(!ctkSource.Token.IsCancellationRequested);

            
        }, ctkSource.Token);
    
        Connected?.Invoke(this, IsConnected);
    }

    public void Disconnect()
    {
        var task = Task.Run(async () =>
        {
            await DisconnectAsync();
        });

        for(int i = 0; i < 10; i++)
        {
            if(task.IsCompleted)break;
            Thread.Sleep(10);
        }
        Connected?.Invoke(this, IsConnected);
    }

    public async Task DisconnectAsync()
    {
        if(readTask != null){
            ctkSource.Cancel();
            await readTask;
        }

        var stream = client.GetStream();
        stream.Close();
        client.Close();
        client.Dispose();
        client = null;
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
            var stream = client.GetStream();
            stream.Write(data, 0, data.Length);
            //stream.Flush();
        }
    }

    public void Send(String s)
    {
        SendData(Encoding.ASCII.GetBytes(s));
    }
}
