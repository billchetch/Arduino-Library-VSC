using System;
using Chetch.Messaging;

namespace Chetch.Arduino.Devices.Comms.CAN;


/*
CANID class parses a Can ID which is  29bit, provided in a 32bit (4 byte) unsigned int.

Message Conversion
The key to this is using the extended ID (so 29 bit) ID value as follows (reading left to right with 4 on the left):

- Byte 4 = First 5 bits are the message type (allowing the type to determin priority)
- Byte 3 = Node and sender 4 bits + 4 bits so 16 nodes and each node can have 16 senders (allowing node and device to determin priority)
- Byte 2 = Tag and CRC: 3 bits for tag and 5 bits for CRC which ic calculated over the data and is provided to guard against SPI issues mainly)
- Byte 1 = Message Timestamp

More on the message structure... in conjuntion with the can frame DLC value, Byte 3 allows for 4 possible arguments with each argument being of max 4 bytes. 
This corresponds to arduino basic types (float, int, long, byte etc.) It also allows for a varilable length argument if there is only 1 arg as we can use the DLC
value to determine

Free single argument example:
DLC = 0 to 8
Byte = 00 00 00 00 : so the first two bits are 00 => 1 argument and the length is provided by the DLC value

2 arguments example (4 bytes and 2 bytes)
DLC = 6
Byte  = 01 (11 01 00)

3 arguments example (3 bytes and 4 bytes 1 byte)
DLC = 8
Byte = 10 (11 01 00)

4 arguments example (2 bytes and 3 bytes 1 byte 2 byte)
DLC = 8
Byte = 11 (01 10 00) (note 2 bytes is inferred as the last argument as 8 - (2 + 3 + 1) = 2)
*/

public class CANID
{
    const byte CRC_GENERATOR = (0b00110101 & 0x1F) << 3;

    public UInt32 ID { get; internal set; } = 0;

    public MessageType Messagetype => (MessageType)(ID >> 24 & 0x1F);

    public byte NodeID => (byte)(ID >> 20 & 0x0F);

    public byte Sender => (byte)(ID >> 16 & 0x0F);

    public byte Tag => (byte)(ID >> 13 & 0x07);

    public byte CRC => (byte)(ID >> 8 & 0x1F);

    public byte Timestamp => (byte)(ID & 0xFF);


    public CANID(UInt32 canId)
    {
        ID = canId;
    }

    byte crc5(byte[] data){
        if(data.Length == 0)return 0;

        //x^5 + x^4 + x^2 + 1 ...
        byte crc = 0;
        for (byte i = 0; i < data.Length; i++) {
            crc ^= data[i];
            for (byte k = 0; k < 8; k++) {
                crc = (byte)((crc & 0x80) != 0 ? (crc << 1) ^ CRC_GENERATOR : crc << 1);
            }
        }
        crc >>= 3;
        return  (byte)(crc & 0x1F);
    }

    bool vcrc5(byte crc, byte[] data){
        return crc == crc5(data);
    }

    public bool ValidateCRC(byte[] data)
    {
        return vcrc5(CRC, data);
    }

    public override string ToString()
    {
        return Chetch.Utilities.Convert.ToBitString(ID);
    }
}
