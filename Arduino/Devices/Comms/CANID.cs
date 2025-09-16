using System;
using Chetch.Messaging;

namespace Chetch.Arduino.Devices.Comms;


/*
CANID class parses a Can ID which is a 29bit object, provided in a 32bit (4 byte) unsigned intt.

We denote a Can ID in bit for as this: xxxx0000-000000000-000000000-000000000 = bit 32-1, byte 4-1. Bits 32,31,30,29 are not used as used by system

Priority = Bits 28,27,26,25 (Bits 4-1 from byte 4 = 4 bits = 16 priorities)
Message Type = Bits 24,23,22,21,20 (bits 8-4 from byte 3 = 5 bits = 32 types)
Message Tag = Bits 19,18,17 (bits 3-1 from byte 3 = 3 bits = 8 tags)
Node ID = Bits 16,15,14,13 (bits 8-5 of byte 2 = 4 bits = 16 nodes)
Sender/Device ID = Bits 12,11,10,9 (bits 4-1 of byte 2 = 4 bits = 16 devices)
Structure = Bits 8-1 (bits 8-1 of byte 1 = 8 bits = 256 possible message structures)

*/
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
