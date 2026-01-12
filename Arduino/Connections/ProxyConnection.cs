using System;
using XmppDotNet.Xmpp.Chatstates;

namespace Chetch.Arduino.Connections;

public class ProxyConnection : IConnection
{
    public IConnection Proxy { get; internal set; }
    public bool IsConnected => Proxy.IsConnected;

    public event EventHandler<byte[]>? DataReceived;
    public event EventHandler<bool>? Connected;

    bool activeProxy = false;

    public ProxyConnection(IConnection proxy, bool active = false, int waitB4connect = 250)
    {
        Proxy = proxy;
        Proxy.Connected += (sender, connected) =>
        {
            if(waitB4connect > 0)
            {
                Thread.Sleep(1000);
            }
            OnConnect(connected);
        };
        activeProxy = active;

        if(activeProxy)
        {
            Proxy.DataReceived += (sender, data) =>
            {
                DataReceived?.Invoke(this, data);
            };
        }
    }

    protected void OnConnect(bool connected)
    {
        Connected?.Invoke(this, connected);
    }

    public void Connect()
    {
        if (activeProxy)
        {
            Proxy.Connect();
        }
    }

    public void Disconnect()
    {
        if (activeProxy)
        {
            Proxy.Disconnect();
        }
    }

    public void Reconnect()
    {
        if (activeProxy)
        {
            Proxy.Reconnect();
        }
        else if(IsConnected)
        {
            OnConnect(true);
        }
    }

    public void SendData(byte[] data)
    {
        if (activeProxy)
        {
            Proxy.SendData(data);
        }
    }
}
