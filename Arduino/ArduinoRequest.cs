using System;
using System.Diagnostics;
using System.Reflection.Metadata;
using XmppDotNet.Xmpp.PubSub;

namespace Chetch.Arduino;

/*
HMMMMM this class isn't currently used as of 1/2/25
*/

public class ArduinoRequest
{
    #region Constants
    public const byte MAX_TAGS = 255;
    public const int DEFAULT_TTL = 30; //in seconds

    public const int CLEANUP_INTERVAL = 2000; // in millis
    #endregion

    #region Static stuff

    static readonly ArduinoRequest?[] requests = new ArduinoRequest[MAX_TAGS];
    static System.Timers.Timer? cleanUpTimer = null;

    static int requestCount = 0;
    static object requestLock = new object();

    
    public static ArduinoRequest Create(ArduinoMessage message)
    {
        
        int i = 0;
        for(i = 0; i < MAX_TAGS; i++)
        {
            var r = requests[i];
            if(r == null)
            {
                break;
            }
            else if(r.HasExpired)
            {
                Release(i);
                break;
            }
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
                if(requestCount > 0){
                    int cleaned = 0;
                    for(int i = 0; i < requests.Length; i++)
                    {
                        var r = requests[i];
                        if(r != null && r.HasExpired)
                        {
                            Release(i);
                        }
                        
                    }
                    Console.WriteLine("Cleaned {0} requests", cleaned);
                }
            };
            cleanUpTimer.Start();
        }

        var req = new ArduinoRequest(i + 1);
        message.Tag = req.Tag;    
        lock(requestLock)
        {
            requests[i] = req;
            requestCount++;
        }
        Console.WriteLine("Adding request {0} at index {1} requestCount is {2}", message.Tag, i, requestCount);
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

        var req = requests[idx];
        if(req == null)
        {
            throw new Exception(String.Format("Request for tag {0} does not exist", message.Tag));
        }

        if(req.HasExpired)
        {
            throw new Exception(String.Format("Request for tag {0} has expired", message.Tag));
        }

        if(requestCount <= 0)
        {
            throw new Exception(String.Format("Unexpected error requestCount {0}", requestCount));
        }

        Release(idx, message);
    }

    static void Release(int idx, ArduinoMessage? message = null){
        if(requests[idx] != null)
        {
            requests[idx]?.Release(message);
            lock(requestLock)
            {
                requests[idx] = null;
                requestCount--;
                Console.WriteLine(String.Format("Released request at index {0}", idx));
            }
        }
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

    public void Release(ArduinoMessage? message)
    {
        released = true;
        if(message != null)
        {
            handler?.Invoke(message);
            handled = true;
        }
    }
    
}
