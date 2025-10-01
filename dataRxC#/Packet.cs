using System;
using System.Net;
public class Packet
{
    public UInt32 Seq { get; set; }
    public UInt64 Timestamp { get; set; }
    public byte[] Content { get; set; } = new byte[244];

    public byte[] Serialize()
    {
        var buffer = new byte[256];
        var span = buffer.AsSpan();

        BitConverter.TryWriteBytes(span.Slice(0, 4), Seq);
        if (BitConverter.IsLittleEndian) span.Slice(0, 4).Reverse();

        BitConverter.TryWriteBytes(span.Slice(4, 8), Timestamp);
        if (BitConverter.IsLittleEndian) span.Slice(4, 8).Reverse();

        Content.AsSpan().CopyTo(span.Slice(12, 244));

        return buffer;
    }
}
