using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditPackages;

public sealed class SelectedSnapshotSourceEnvelopeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldPreserveExactLegacyLogicalBytesGivenSelectedFamilyWithoutBusinessBlobs(bool close)
    {
        // Arrange
        var source = Encoding.UTF8.GetBytes("bdgrz.snapshot.source.test.v1\0{\"events\":[]}");
        using var capture = SnapshotSourceCapture.Begin(Uuid.CreateVersion4());
        var logical = Convert.ToHexStringLower(SnapshotSourceCapture.HashData(source));
        Assert.True(capture.TryGet(logical, out var original));

        // Act
        Capture(close, logical, source, []);
        Assert.True(capture.TryGet(logical, out var result));

        // Assert
        Assert.Equal(original!.Blob, result!.Blob);
        Assert.Equal(source, File.ReadAllBytes(result.Path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRetainLogicalPreimageAndDeduplicatedVerifiedBytesGivenSelectedFamilyBodies(bool close)
    {
        // Arrange
        var source = Encoding.UTF8.GetBytes("bdgrz.snapshot.source.test.v1\0{\"events\":[1]}");
        var bytes = Encoding.UTF8.GetBytes("{\"narrative\":\"Exact selected business bytes\"}");
        var reference = new ImmutableContentRef(Uuid.CreateVersion4(), Convert.ToHexStringLower(SHA256.HashData(bytes)),
            bytes.LongLength, "application/json", "source-business-test.v1");
        using var capture = SnapshotSourceCapture.Begin(Uuid.CreateVersion4());
        var logical = Convert.ToHexStringLower(SnapshotSourceCapture.HashData(source));

        // Act
        Capture(close, logical, source, [new(reference, bytes), new(reference, bytes)]);
        Assert.True(capture.TryGet(logical, out var result));
        var physical = File.ReadAllBytes(result!.Path);
        using var document = JsonDocument.Parse(physical);
        var blob = Assert.Single(document.RootElement.GetProperty("content_blobs").EnumerateArray());

        // Assert
        Assert.Equal(logical, document.RootElement.GetProperty("logical_content_sha256").GetString());
        Assert.Equal(source, Convert.FromBase64String(document.RootElement.GetProperty("source_base64").GetString()!));
        Assert.Equal(bytes, Convert.FromBase64String(blob.GetProperty("base64").GetString()!));
        Assert.Equal(result.Blob.Sha256, Convert.ToHexStringLower(SHA256.HashData(physical)));
        Assert.Equal(result.Blob.Bytes, physical.LongLength);
        Assert.Equal(close ? "application/vnd.bdgrz.type-ii-close-snapshot-source.v1+json" :
            "application/vnd.bdgrz.finding-snapshot-source.v1+json", result.Blob.MediaType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRefuseMismatchedBusinessBlobGivenSelectedSourceEnvelope(bool close)
    {
        // Arrange
        var source = Encoding.UTF8.GetBytes("bdgrz.snapshot.source.test.v1\0{\"events\":[]}");
        byte[] bytes = [1, 2];
        var reference = new ImmutableContentRef(Uuid.CreateVersion4(), Convert.ToHexStringLower(SHA256.HashData(bytes)),
            2, "application/json", "source-business-test.v1");
        using var capture = SnapshotSourceCapture.Begin(Uuid.CreateVersion4());
        var logical = Convert.ToHexStringLower(SnapshotSourceCapture.HashData(source));

        // Act
        var failure = Assert.Throws<ImmutableContentIntegrityException>(() =>
            Capture(close, logical, source, [new(reference, [2, 1])]));

        // Assert
        Assert.NotEmpty(failure.Message);
        Assert.True(capture.TryGet(logical, out var preserved));
        Assert.Equal(source, File.ReadAllBytes(preserved!.Path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldPreserveLogicalSourcePayloadGivenIgnoredSelectedPhysicalBytes(bool close)
    {
        // Arrange
        using var json = JsonDocument.Parse("{\"exact\":true}");
        var eventId = Uuid.CreateVersion4();
        var payload = json.RootElement.Clone();
        byte[] body = [1, 2];
        var reference = new ImmutableContentRef(Uuid.CreateVersion4(), Convert.ToHexStringLower(SHA256.HashData(body)),
            2, "application/json", "source-business-test.v1");
        var blob = new SnapshotSourceContentBlob(reference, body);
        var finding = new FindingTypeIBaselineSourceEvent(eventId, 1, 1, DateTimeOffset.UnixEpoch, "finding", payload);
        var operating = new TypeIIPeriodCloseNativeSource(new NativeFactReference(new EventStreamAddress("tenant", "area", "owner"),
            eventId, 1), "operating", new string('a', 64), payload);
        object before = close ? operating : finding;
        object after = close ? operating with { PhysicalContentBlobs = [blob] } : finding with { ContentBlobs = [blob] };

        // Act
        var original = JsonSerializer.SerializeToUtf8Bytes(before, ComplianceCoreJsonContext.Default.GetTypeInfo(before.GetType())!);
        var captured = JsonSerializer.SerializeToUtf8Bytes(after, ComplianceCoreJsonContext.Default.GetTypeInfo(after.GetType())!);

        // Assert
        Assert.Equal(original, captured);
        Assert.DoesNotContain("content_blobs", Encoding.UTF8.GetString(captured), StringComparison.Ordinal);
    }

    static void Capture(bool close, string logical, byte[] source, IReadOnlyList<SnapshotSourceContentBlob> blobs)
    {
        if (close)
            SnapshotSourceCapture.TypeIIClose(logical, source, blobs);
        else
            SnapshotSourceCapture.Finding(logical, source, blobs);
    }
}
