using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public struct Frame
{
    public bool final;
    public int RSV1, RSV2, RSV3;
    public OpCode opCode;
    public bool masked;
    public int payloadLength;
    public byte[] key;
    public byte[] payload;

    public bool AllEmpty
    {
        get
        {
            return !final && RSV1 == 0 && RSV2 == 0 && RSV3 == 0 && opCode == 0 && !masked && payloadLength == 0;
        }
    }

    public bool isValid()
    {
        return RSV1 == 0 && RSV2 == 0 && RSV3 == 0 && opCode != OpCode.Reserved;
    }

    public static async Task<Frame> convertToFrame(int length, byte[] buffer, NetworkStream stream)
    {
        int offset = 6;

        Frame frame = new Frame();

        frame.final = (int)(byte)(buffer[0] & 0b10000000) > 0;
        frame.RSV1 = (int)(byte)(buffer[0] & 0b01000000);
        frame.RSV2 = (int)(byte)(buffer[0] & 0b00100000);
        frame.RSV3 = (int)(byte)(buffer[0] & 0b00010000);
        int opCode = (int)(byte)(buffer[0] & 0b00001111);
        if (opCode > 2 && opCode < 8 || opCode > 10)
        {
            frame.opCode = OpCode.Reserved;
        }
        else
        {
            frame.opCode = (OpCode)opCode;
        }

        if (!frame.isValid())
        {
            return frame;
        }

        frame.masked = (int)(byte)(buffer[1] & 0b10000000) > 0;
        frame.payloadLength = (int)(byte)(buffer[1] & 0b01111111);

        frame.key = new byte[4];
        Array.Copy(buffer, 2, frame.key, 0, 4);

        if (frame.payloadLength == 126)
        {
            offset += 2;

            var payLoadLength = new byte[2]
            {
                buffer[3],
                buffer[2],
            };

            frame.payloadLength = BitConverter.ToUInt16(payLoadLength, 0);

            Array.Copy(buffer, 4, frame.key, 0, 4);
        }

        if (frame.payloadLength <= 0)
        {
            Logger.Warn("Invalid Frame!");
            return frame;
        }

        frame.payload = new byte[frame.payloadLength];
        int trueSize = 0;

        if (length - offset > 0)
        {
            Array.Copy(buffer, offset, frame.payload, 0, length - offset);
            trueSize = length - offset;
        }

        while (trueSize < frame.payloadLength)
        {
            try
            {
                length = await stream.ReadAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                Array.Copy(buffer, 0, frame.payload, trueSize, length > frame.payloadLength - trueSize ? frame.payloadLength - trueSize : length);
                trueSize += length;
            }
            catch
            {
                Logger.Error("Error reading stream!");
            }
        }

        // Unmask the payload data
        for (int i = 0; i < frame.payload.Length; i++)
        {
            frame.payload[i] ^= frame.key[i % 4];
        }

        return frame;
    }
}

public enum OpCode
{
    Continuation = 0,
    Text = 1,
    Binary = 2,
    Close = 8,
    Ping = 9,
    Pong = 10,
    Reserved = 16
}
