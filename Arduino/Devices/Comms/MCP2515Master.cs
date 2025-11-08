using System;
using Chetch.Messaging;

namespace Chetch.Arduino.Devices.Comms;

public class MCP2515Master : MCP2515
{

    #region Constants
    private const byte MESSAGE_ID_FORWARD_RECEIVED = 100;
    private const byte MESSAGE_ID_FORWARD_SENT = 101;
    #endregion

    #region Classes and Enums
    public enum BusMessageDirection
    {
        OUTBOUND,
        INBOUND
    }

    public class BusMessageEventArgs
    {
        public CANID CanID { get; internal set; }

        public byte NodeID => CanID.NodeID;

        public byte CanDLC => (byte)CanData.Length;

        public byte[] CanData { get; }

        public ArduinoMessage Message { get; } = new ArduinoMessage();

        public BusMessageDirection Direction { get; internal set; }

        public BusMessageEventArgs(ArduinoMessage message)
        {
            if (message.Tag == MESSAGE_ID_FORWARD_SENT)
            {
                Direction = BusMessageDirection.OUTBOUND;
            }
            else if (message.Tag == MESSAGE_ID_FORWARD_RECEIVED)
            {
                Direction = BusMessageDirection.INBOUND;
            }

            Message.Sender = message.Sender;
            Message.Target = message.Target;
            CanData = message.Get<byte[]>(0);
            CanID = new CANID(message.Get<UInt32>(1));
            Message.Type = message.Get<MessageType>(2);
            Message.Tag = CanID.Tag;
        }
    }
    #endregion

    #region Events
    public EventHandler<BusMessageEventArgs>? BusMessageReceived;

    #endregion

    #region Properties
    #endregion

    #region Constructors
    public MCP2515Master(byte nodeID, string? name = null) : base(nodeID, name)
    {}
    #endregion

    #region Messaging
    override public ArduinoMessageMap.UpdatedProperties HandleMessage(ArduinoMessage message)
    {
        switch (message.Type)
        {
            //Message of this type are assumed to be 'forwarded' bus messages
            case MessageType.INFO:
                if (message.Tag == MESSAGE_ID_FORWARD_SENT || message.Tag == MESSAGE_ID_FORWARD_RECEIVED)
                {
                    var eargs = new BusMessageEventArgs(message);
                    BusMessageReceived?.Invoke(this, eargs);
                }
                break;

        }
        return base.HandleMessage(message);
    }

    public void RequestRemoteNodesStatus()
    {
        SendCommand(DeviceCommand.REQUEST, (byte)MessageType.STATUS_REQUEST);
    }

    public void PingRemoteNode(byte nodeID)
    {
        SendCommand(DeviceCommand.REQUEST, (byte)MessageType.PING, nodeID);
    }
    #endregion
}