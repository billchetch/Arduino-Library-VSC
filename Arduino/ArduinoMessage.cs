using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Messaging;
using Chetch.Utilities;

namespace Chetch.Arduino;

/// <summary>
/// Messages sent to board
/// </summary>
public class ArduinoMessage
{
    #region Consttants
    public const byte NO_TARGET = 0;
    public const byte NO_SENDER = 0;
    #endregion

    static public ArduinoMessage Deserialize(byte[] bytes)
    {
        var msg = new ArduinoMessage();
        msg.ReadBytes(bytes);
        return msg;
    }

    public MessageType Type { get; set; }
    public byte Tag { get; set; } = 0; //can be used to track messages
    public byte Target { get; set; } = 0; //ID number on board to determine what is beig targeted
    public byte Sender { get; set; } = 0; //
    public List<byte[]> Arguments { get; } = new List<byte[]>();
    public bool LittleEndian { get; set; } = true;

    //Convenience properties
    public bool IsCommand => Type == MessageType.COMMAND;
    public bool IsData => Type == MessageType.DATA;

    public bool IsCommandRelated => Type == MessageType.COMMAND_RESPONSE || Type == MessageType.COMMAND;

    public bool IsConfigRelated => Type == MessageType.CONFIGURE || Type == MessageType.CONFIGURE_RESPONSE;

    public bool IsInitRelated => Type == MessageType.INITIALISE || Type == MessageType.INITIALISE_RESPONSE;


    public ArduinoMessage(){}
    public ArduinoMessage(MessageType type)
    {
        Type = type;
    }

    public dynamic Get(int idx, Type? type = null)
    {
        if (type == null) type = typeof(Object);

        return Chetch.Utilities.Convert.ToType(type, Arguments[idx], LittleEndian);
    }

    public T Get<T>(int idx)
    {
        return Chetch.Utilities.Convert.To<T>(Arguments[idx], LittleEndian);
    }

    public void Add(byte[] bytes)
    {
        Arguments.Add(bytes);
    }

    public void Add(byte b)
    {
        Add(new byte[] { b });
    }

    public void Add(bool b)
    {
        Add(b ? (byte)1 : (byte)0);
    }

    public void Add(String s)
    {
        Add(Chetch.Utilities.Convert.ToBytes(s));
    }

    public void Add(Int16 arg)
    {
        byte[] bytes = Chetch.Utilities.Convert.ToBytes(arg, LittleEndian, true, -1);
        Add(bytes);
    }

    public void Add(UInt16 arg)
    {
        byte[] bytes = Chetch.Utilities.Convert.ToBytes(arg, LittleEndian, true, -1);
        Add(bytes);
    }

    public void Add(int arg)
    {
        byte[] bytes = Chetch.Utilities.Convert.ToBytes((Int16)arg, LittleEndian, true, -1);
        Add(bytes);
    }

    public void Add(long arg)
    {
        byte[] bytes = Chetch.Utilities.Convert.ToBytes((Int32)arg, LittleEndian, true, -1);
        Add(bytes);
    }

    public void Add(ulong arg)
    {
        byte[] bytes = Chetch.Utilities.Convert.ToBytes((UInt32)arg, LittleEndian, true, -1);
        Add(bytes);
    }

    public void Add(Enum arg)
    {
        Add(System.Convert.ToInt32(arg));
    }

    public void Add(Object arg)
    {
        var bytes = Chetch.Utilities.Convert.ToBytes(arg);
        Add(bytes);
    }
    public void WriteBytes(List<byte> bytes)
    {

        //1. Add member vars
        bytes.Add((byte)Type);
        bytes.Add(Tag);
        bytes.Add(Target);
        bytes.Add(Sender);

        //2. add arguments (length of argument followed by argment bytes)
        foreach (var b in Arguments)
        {
            bytes.Add((byte)b.Length);
            bytes.AddRange(b);
        }
    }

    public int GetByteCount()
    {
        int byteCount = 4; //bytes for type, tag, target and sender
        foreach (var b in Arguments)
        {
            byteCount += 1 + b.Length;
        }
        return byteCount;
    }

    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        WriteBytes(bytes);
        return bytes.ToArray();
    }

    public void ReadBytes(byte[] bytes)
    {
        Type = (Chetch.Messaging.MessageType)bytes[0];
        Tag = bytes[1];
        Target = bytes[2];
        Sender = bytes[3];

        //... and convert arguments
        int argumentIndex = 4;
        while (argumentIndex < bytes.Length)
        {
            int length = bytes[argumentIndex];
            byte[] arg = new byte[length];
            for (int i = 0; i < length; i++)
            {
                arg[i] = bytes[argumentIndex + i + 1];
            }
            Add(arg);
            argumentIndex += length + 1;
        }
    }
}

