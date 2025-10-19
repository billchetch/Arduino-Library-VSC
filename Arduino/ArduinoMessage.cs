using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Chetch.Messaging;
using Chetch.Utilities;

namespace Chetch.Arduino;

/// <summary>
/// Messages sent to and received from board
/// </summary>
public class ArduinoMessage : IMessageQueueItem<ArduinoMessage>
{
    #region Consttants
    public const byte NO_TARGET = 0;
    public const byte NO_SENDER = 0;
    #endregion

    #region Static methods
    static public ArduinoMessage Deserialize(byte[] bytes, MessageEncoding encoding)
    {
        var msg = new ArduinoMessage();
        msg.ReadBytes(bytes);
        return msg;
    }

    static public byte[] Serialize(ArduinoMessage message, MessageEncoding encoding)
    {
        return message.Serialize();
    }

    private static int GetSizeOf<T>()
    {
        return Marshal.SizeOf(typeof(T));
    }   
    #endregion

    #region Properties
    public MessageType Type { get; set; }
    public byte Tag { get; set; } = 0; //can be used to track messages
    public byte Target { get; set; } = 0; //ID number on board to determine what is beig targeted
    public byte Sender { get; set; } = 0; //
    public List<byte[]?> Arguments { get; } = new List<byte[]?>();
    public bool LittleEndian { get; set; } = true;

    //Convenience properties
    public bool IsCommand => Type == MessageType.COMMAND;
    public bool IsData => Type == MessageType.DATA;

    public bool IsCommandRelated => Type == MessageType.COMMAND_RESPONSE || Type == MessageType.COMMAND;

    public bool IsConfigRelated => Type == MessageType.CONFIGURE || Type == MessageType.CONFIGURE_RESPONSE;

    public bool IsInitRelated => Type == MessageType.INITIALISE || Type == MessageType.INITIALISE_RESPONSE;

    public DateTime Created { get; internal set; }
    #endregion

    #region Constructors
    public ArduinoMessage()
    {
        Created = DateTime.Now;
    }
    public ArduinoMessage(MessageType type)
    {
        Type = type;
        Created = DateTime.Now;
    }
    #endregion

    #region Get Value Methods
    public dynamic Get(int idx, Type? type = null)
    {
        if (type == null) type = typeof(Object);

        return Chetch.Utilities.Convert.ToType(type, Arguments[idx], LittleEndian);
    }

    public T Get<T>(int idx)
    {
        return Chetch.Utilities.Convert.To<T>(Arguments[idx], LittleEndian);
    }

    public T GetFirst<T>()
    {
        return Get<T>(0);
    }

    public T GetLast<T>()
    {
        return Get<T>(Arguments.Count - 1);
    }
    #endregion

    #region Set/Add value methods
    public void Add(byte[] bytes, int idx = -1)
    {
        if (idx == -1)
        {
            Arguments.Add(bytes);
        }
        else if (idx >= 0)
        {
            if (idx < Arguments.Count)
            {
                Arguments[idx] = bytes;
            }
            else
            {
                for (int i = Arguments.Count; i < idx; i++)
                {
                    Arguments.Add(null);
                }
                Arguments.Add(bytes);
            }
        }
    }

    public void Add(byte b, int idx = -1)
    {
        Add(new byte[] { b }, idx);
    }

    public void Add(bool b, int idx = -1)
    {
        Add(b ? (byte)1 : (byte)0, idx);
    }

    public void Add(String s, int idx = -1)
    {
        Add(Chetch.Utilities.Convert.ToBytes(s), idx);
    }

    public void Add(Int16 arg, int idx = -1)
    {
        byte[] bytes = Chetch.Utilities.Convert.ToBytes(arg, LittleEndian, true, -1);
        Add(bytes, idx);
    }

    public void Add(UInt16 arg, int idx = -1)
    {
        byte[] bytes = Chetch.Utilities.Convert.ToBytes(arg, LittleEndian, true, -1);
        Add(bytes, idx);
    }

    public void Add(int arg, int idx = -1)
    {
        byte[] bytes = Chetch.Utilities.Convert.ToBytes((Int16)arg, LittleEndian, true, -1);
        Add(bytes, idx);
    }

    public void Add(uint arg, int idx = -1)
    {
        Add((UInt16)arg, idx);
    }

    public void Add(long arg, int idx = -1)
    {
        byte[] bytes = Chetch.Utilities.Convert.ToBytes((Int32)arg, LittleEndian, true, -1);
        Add(bytes, idx);
    }

    public void Add(ulong arg, int idx = -1)
    {
        byte[] bytes = Chetch.Utilities.Convert.ToBytes((UInt32)arg, LittleEndian, true, -1);
        Add(bytes, idx);
    }

    public void Add(Enum arg, int idx = -1)
    {
        Add(System.Convert.ToInt32(arg), idx);
    }

    public void Add(Object arg, int idx = -1)
    {
        var bytes = Chetch.Utilities.Convert.ToBytes(arg);
        Add(bytes, idx);
    }

    public int Populate<T>(byte[] data, int n = 0)
    {
        int k = n + GetSizeOf<T>();
        Arguments.Add(data[n..k]);
        return k;
    }

    public int Populate<T1, T>(byte[] data)
    {
        return Populate<T>(data, Populate<T1>(data));
    }
    public int Populate<T1, T2, T>(byte[] data)
    {
        return Populate<T>(data, Populate<T1, T2>(data));
    }

    public int Populate<T1, T2, T3, T>(byte[] data)
    {
        return Populate<T>(data, Populate<T1, T2, T3>(data));
    }
    #endregion

    #region Serialization methods
    public void WriteBytes(List<byte> bytes)
    {

        //1. Add member vars
        bytes.Add((byte)Type);
        bytes.Add(Tag);
        bytes.Add(Target);
        bytes.Add(Sender);

        //2. add arguments (length of argument followed by argment bytes)
        int idx = 0;
        foreach (var b in Arguments)
        {
            if (b == null)
            {
                throw new Exception(String.Format("Argument at index {0} is null", idx));
            }
            bytes.Add((byte)b.Length);
            bytes.AddRange(b);
            idx++;
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
        int bytesTotal = 4;
        while (argumentIndex < bytes.Length)
        {
            int length = bytes[argumentIndex];
            if (length > bytes.Length - argumentIndex - 1)
            {
                throw new IndexOutOfRangeException(
                    String.Format("Argument index {0}={1} but there are only {2} bytes remaining",
                                    argumentIndex,
                                    length,
                                    bytes.Length - argumentIndex - 1));
            }
            byte[] arg = new byte[length];
            for (int i = 0; i < length; i++)
            {
                arg[i] = bytes[argumentIndex + i + 1];
            }
            Add(arg);
            argumentIndex += length + 1; //go to next argument
            bytesTotal += length;
        }
    }
    #endregion
}

