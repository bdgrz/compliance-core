using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using Cntryl.Fitz;

namespace Bdgrz.Compliance.Tests.Hosting.Persistence;

public sealed class PinnedFitzFrameTests
{
    [Theory]
    [InlineData("StreamAppend", 0)]
    [InlineData("StreamAppend", 1)]
    [InlineData("KvPut", 0)]
    [InlineData("KvPut", 1)]
    [InlineData("KvInsert", 0)]
    [InlineData("KvInsert", 1)]
    public void ShouldEnforcePinnedWireBoundaryGivenCompleteOperation(string operation, int excess)
    {
        // Arrange
        var stream = operation == "StreamAppend";
        var route = Encoding.UTF8.GetBytes(stream
            ? "stream://ténant/events/récord" : "kv://ténant/records/évidence");
        var key = Encoding.UTF8.GetBytes("clé");
        var overhead = route.Length + (stream ? 26 : key.Length + 20);
        var content = new byte[ushort.MaxValue - overhead + excess];
        var payload = OperationPayload(stream, route, key, content);
        Assert.Equal(ushort.MaxValue + excess, payload.Length);

        // Act
        var failure = Record.Exception(() => Encode(operation, payload));

        // Assert
        if (excess == 0)
        {
            Assert.Null(failure);
            Assert.Equal(ushort.MaxValue + (stream ? 16 : 14), Encode(operation, payload).Length);
        }
        else
        {
            Assert.NotNull(failure);
            Assert.Contains("65535-byte Fitz wire limit", failure.Message, StringComparison.Ordinal);
        }
    }

    internal static byte[] OperationPayload(bool stream, byte[] route, byte[] key, byte[] content)
    {
        using var payload = new MemoryStream();
        Span<byte> identity = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(identity, 1);
        payload.Write(identity); // Session/transaction identity.
        if (stream)
        {
            payload.Write(new byte[8]); // Expected physical offset.
            WriteBytes(payload, content);
            payload.WriteByte(1); // Route metadata is present.
            WriteBytes(payload, route);
            payload.WriteByte(0); // Portia sends no separate discriminator.
        }
        else
        {
            WriteBytes(payload, route);
            WriteBytes(payload, key);
            WriteBytes(payload, content);
        }
        return payload.ToArray();
    }

    static void WriteBytes(MemoryStream target, byte[] value)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)value.Length);
        target.Write(length);
        target.Write(value);
    }

    internal static byte[] Encode(string operation, byte[] payload)
    {
        var assembly = typeof(FitzLimits).Assembly;
        var codec = assembly.GetType("Cntryl.Fitz.Protocol.FrameCodec", throwOnError: true)!;
        var encode = codec.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Single(static method => method.Name == "EncodeCorrelated").CreateDelegate<FrameEncoder>();
        var messages = assembly.GetType("Cntryl.Fitz.Protocol.MessageTypes", throwOnError: true)!;
        var opcode = (ushort)messages.GetField(operation,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;
        return encode(1, opcode, payload);
    }

    delegate byte[] FrameEncoder(ulong correlation, ushort opcode, ReadOnlySpan<byte> payload);
}
