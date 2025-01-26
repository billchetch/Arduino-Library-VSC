using System;
using System.Diagnostics;
using System.Reflection.Metadata;
using XmppDotNet.Xmpp.PubSub;

namespace Chetch.Arduino;

public class ArduinoRequest
{
    #region Constants
    public const byte MAX_TAGS = 255;
    public const int DEFAULT_TTL = 30; //in seconds

    public const int CLEANUP_INTERVAL = 2000; // in millis
    #endregion

    #region Static stuff

    static readonly ArduinoRequest[] requests = new ArduinoRequest[MAX_TAGS];
    static System.Timers.Timer? cleanUpTimer = null;

    public static ArduinoRequest Create(ArduinoMessage message)
    {
        byte i = 0;
        while(i < MAX_TAGS && requests[i] != null && !requests[i].HasExpired)
        {
            i++;
        }
        if(i >= MAX_TAGS)
        {
            throw new Exception(String.Format("Cannot create request as max of {0} tags already exist", MAX_TAGS));
        }

        if(cleanUpTimer == null)
        {
            cleanUpTimer = new System.Timers.Timer();
            cleanUpTimer.AutoReset = true;
            cleanUpTimer.Interval = CLEANUP_INTERVAL;
            cleanUpTimer.Elapsed += (sender, eargs) => {
                //Console.WriteLine("Cleanup timer called...");
                foreach(var req in requests)
                {
                    if(req != null && req.HasExpired)
                    {
                        req.Release();
                    }
                }
            };
            cleanUpTimer.Start();
        }

        var req = new ArduinoRequest(i + 1);
        requests[i] = req;
        message.Tag = req.Tag;
        return req;
    }

    public static void Handle(ArduinoMessage message)
    {
        if(message.Tag == 0)
        {
            return;
        }

        byte idx = (byte)(message.Tag - 1);

        if(idx >= MAX_TAGS)
        {
            throw new Exception(String.Format("Tag {0} is not valid ", message.Tag));
        }

        if(requests[idx] == null)
        {
            throw new Exception(String.Format("Request for tag {0} does not exist", message.Tag));
        }

        if(requests[idx].HasExpired)
        {
            throw new Exception(String.Format("Request for tag {0} has expired", message.Tag));
        }

        requests[idx].Release(message);
    }

    #endregion

    public byte Tag = 0;

    public int TTL = DEFAULT_TTL;

    public DateTime Created = DateTime.Now;
    
    Action<ArduinoMessage>? handler;

    bool handled = false;
    bool released = false;
    public bool HasExpired => released || (DateTime.Now - Created).TotalSeconds > TTL;

    public ArduinoRequest(int tag)
    {
        Tag = (byte)tag;
    }
    

    public Task<bool> OnReceivedAsync(Action<ArduinoMessage> action, int ttl = DEFAULT_TTL)
    {
        OnReceived(action, ttl);

        return Task.Run<bool>(()=>{
            while(!released){
                Thread.Sleep(100);
            }
            return handled;
        });
    }

    public void OnReceived(Action<ArduinoMessage> action, int ttl = DEFAULT_TTL)
    {
        handled = false;
        handler = action;
        TTL = ttl;
    }

    public void Release(ArduinoMessage message)
    {
        Release();
        handler?.Invoke(message);
        handled = true;
    }

    public void Release(){
        released = true;
    }
}
