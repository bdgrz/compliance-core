using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Tenants.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.NativeOperations;

public sealed class NativeReplayCommitmentTests
{
    [Fact]
    public void ShouldUseWireEventKindGivenNativeReplayCommitment()
    {
        // Arrange
        var tenant = Uuid.Parse("00000000-0000-4000-8000-000000000001", CultureInfo.InvariantCulture);
        var fact = new TenantRegistered(tenant, tenant, "Example", "example");
        fact.AttachMetadata(new DomainEventMetadata(tenant, tenant, 1, DateTimeOffset.UnixEpoch));
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        static void Add(IncrementalHash hash, ReadOnlySpan<byte> value)
        {
            Span<byte> size = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(size, value.Length);
            hash.AppendData(size);
            hash.AppendData(value);
        }
        Add(hash, Encoding.UTF8.GetBytes(NativeReplayCommitment.Empty));
        Add(hash, Encoding.UTF8.GetBytes("bdgrz.tenant.registered@1"));
        Add(hash, JsonSerializer.SerializeToUtf8Bytes(fact, ComplianceCoreJsonContext.Default.GetTypeInfo(fact.GetType())!));
        Add(hash, Encoding.UTF8.GetBytes(fact.Metadata.EventId.ToString()));
        Add(hash, Encoding.UTF8.GetBytes(fact.Metadata.AggregateId.ToString()));
        Add(hash, Encoding.UTF8.GetBytes(fact.Metadata.AggregateVersion.ToString(CultureInfo.InvariantCulture)));
        Add(hash, Encoding.UTF8.GetBytes(fact.Metadata.OccurredOn.ToString("O", CultureInfo.InvariantCulture)));
        Add(hash, [0]);
        var expected = Convert.ToHexStringLower(hash.GetHashAndReset());

        // Act
        var actual = NativeReplayCommitment.Append(NativeReplayCommitment.Empty, fact);

        // Assert
        Assert.Equal(expected, actual);
    }
}
