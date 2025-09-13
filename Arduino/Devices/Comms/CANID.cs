using System;
using Chetch.Messaging;

namespace Chetch.Arduino.Devices.Comms;

public class CANID
{
    public enum CANMessagePriority
    {
        CAN_PRIORITY_RANDOM = 0,
        CAN_PRIORITY_CRITICAL,
        CAN_PRIORITY_HIGH,
        CAN_PRIORITY_NORMAL,
        CAN_PRIORITY_LOW
    };

    public UInt32 ID { get; internal set; } = 0;

    public CANMessagePriority Priority => (CANMessagePriority)(ID >> 24 & 0x0F);

    public MessageType Messagetype => (MessageType)((ID >> 19) & 0x1F);

    public byte Tag => (byte)((ID >> 16) & 0x07);

    public byte NodeID => (byte)(ID >> 12 & 0x0F);

    public byte Sender => (byte)(ID >> 8 & 0x0F);

    public byte MessageStructure => (byte)(ID & 0xFF);

    public CANID(UInt32 canId)
    {
        ID = canId;
    }
}
