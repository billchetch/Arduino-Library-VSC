using System;
using Chetch.Messaging;
using XmppDotNet.Xmpp.HttpUpload;

namespace Chetch.Arduino.Devices.Comms;

public class MCP2515 : ArduinoDevice
{
    #region Classes and Enums
    public class ForwardedMessageEventArgs
    {
        public UInt32 CanID { get; internal set; } = 0;

        public byte CanDLC { get; internal set; } = 0;

        public List<byte> CanData { get; } = new List<byte>();

        public ArduinoMessage Message { get; } = new ArduinoMessage();

        public ForwardedMessageEventArgs(ArduinoMessage message)
        {
            Message.Sender = message.Sender;
            Message.Target = message.Target;
            Message.Tag = message.Tag;
            int argCount = message.Arguments.Count;

            //Last 3 arguments of the message forwarded are 'meta' data which we extract
            CanID = message.Get<UInt32>(argCount - 3); //last but two
            CanDLC = message.Get<byte>(argCount - 2); //last but one
            Message.Type = message.Get<MessageType>(argCount - 1); //last argument
            for (int i = 0; i < argCount - 3; i++)
            {
                byte[]? bytes = message.Arguments[i];
                if (bytes != null)
                {
                    Message.Add(bytes);
                    CanData.AddRange(bytes);
                }
            }
        }

        public void Validate()
        {
            //TODO: throws some exceptions when called to facilitate logging
        }
    }
    #endregion

    #region Properties
    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 1)]
    public bool CanReceiveMessages { get; internal set; } = false;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 2)]
    public bool CanReceiveErrors { get; internal set; } = false;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 3)]
    public byte NodeID { get; internal set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 4)]
    [ArduinoMessageMap(Messaging.MessageType.DATA, 0)]
    public byte StatusFlags { get; internal set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 5)]
    [ArduinoMessageMap(Messaging.MessageType.DATA, 1)]
    [ArduinoMessageMap(Messaging.MessageType.ERROR, 2)]
    public byte ErrorFlags { get; internal set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 6)]
    [ArduinoMessageMap(Messaging.MessageType.DATA, 2)]
    [ArduinoMessageMap(Messaging.MessageType.ERROR, 3)]
    public byte TXErrorCount { get; internal set; } = 0;

    [ArduinoMessageMap(Messaging.MessageType.STATUS_RESPONSE, 7)]
    [ArduinoMessageMap(Messaging.MessageType.DATA, 3)]
    [ArduinoMessageMap(Messaging.MessageType.ERROR, 4)]
    public byte RXErrorCount { get; internal set; } = 0;
    #endregion

    #region Events
    public EventHandler<ForwardedMessageEventArgs>? MessageForwarded;
    #endregion

    #region Constructors
    public MCP2515(string sid, string? name = null) : base(sid, name)
    {

    }
    #endregion

    #region Messaging
    override public ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        switch (message.Type)
        {
            //Message of this type are assumed to be 'forwarded' messages
            case Messaging.MessageType.INFO:
                //Seperate messages
                MessageForwarded?.Invoke(this, new ForwardedMessageEventArgs(message));
                break;
        }
        return base.HandleMessage(message);
    }
    #endregion
}
