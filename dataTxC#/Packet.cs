using System;
using System.Net;
public class Packet
{
    public UInt32 Seq { get; set; }
    public UInt64 Timestamp { get; set; }
    public byte[] Content { get; set; } = new byte[244];

    // 빅엔디안으로 직렬화
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

    public Packet Deserialize(byte[] data)
    {
        if (data.Length != 256) throw new ArgumentException("Invalid packet size");

        var span = data.AsSpan();
        if (BitConverter.IsLittleEndian)
        {
            span.Slice(0, 4).Reverse();
            span.Slice(4, 8).Reverse();
        }
        Seq = BitConverter.ToUInt32(span.Slice(0, 4));
        Timestamp = BitConverter.ToUInt64(span.Slice(4, 8));
        Content = span.Slice(12, 244).ToArray();
        return this;
    }
}
