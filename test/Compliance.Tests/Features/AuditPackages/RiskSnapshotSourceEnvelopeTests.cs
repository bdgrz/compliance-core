using Bdgrz.Compliance.Features.Proofs.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditPackages;

public sealed class RiskSnapshotSourceEnvelopeTests
{
    [Fact]
    public void ShouldKeepCanonicalLegacyBytesGivenNoImmutableRiskBodies()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes("bdgrz.snapshot.source.risk.v1\0{\"events\":[]}");
        using var capture = SnapshotSourceCapture.Begin(Uuid.CreateVersion4());
        var digest = Convert.ToHexStringLower(SnapshotSourceCapture.HashData(bytes));

        // Act
        SnapshotSourceCapture.Risk(digest, bytes, []);

        // Assert
        Assert.True(capture.TryGet(digest, out var material));
        Assert.Equal(digest, material!.Blob.Sha256);
        Assert.Equal(bytes, File.ReadAllBytes(material.Path));
        Assert.Equal("snapshot", material.Blob.FileExtension);
    }

    [Fact]
    public void ShouldRetainExactLogicalPreimageGivenVerifiedImmutableRiskBodies()
    {
        // Arrange
        var source = Encoding.UTF8.GetBytes("bdgrz.snapshot.source.risk.v1\0{\"events\":[{\"version\":1}]}");
        var body = Encoding.UTF8.GetBytes("{\"title\":\"Exact risk narrative\"}");
        var reference = Reference(body);
        using var capture = SnapshotSourceCapture.Begin(Uuid.CreateVersion4());
        var digest = Convert.ToHexStringLower(SnapshotSourceCapture.HashData(source));

        // Act
        SnapshotSourceCapture.Risk(digest, source, [new(reference, body)]);
        Assert.True(capture.TryGet(digest, out var material));
        var physical = File.ReadAllBytes(material!.Path);
        using var json = JsonDocument.Parse(physical);

        // Assert
        Assert.Equal(digest, Convert.ToHexStringLower(SHA256.HashData(source)));
        Assert.Equal(source, Convert.FromBase64String(json.RootElement.GetProperty("source_base64").GetString()!));
        Assert.Equal(digest, json.RootElement.GetProperty("logical_content_sha256").GetString());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(physical)), material.Blob.Sha256);
        Assert.NotEqual(digest, material.Blob.Sha256);
        var blob = Assert.Single(json.RootElement.GetProperty("content_blobs").EnumerateArray());
        Assert.Equal(body, Convert.FromBase64String(blob.GetProperty("base64").GetString()!));
        Assert.Equal(reference.Sha256, blob.GetProperty("reference").GetProperty("sha256").GetString());
    }

    [Fact]
    public void ShouldBuildIdenticalPhysicalEnvelopeGivenReorderedRepeatedRiskReferences()
    {
        // Arrange
        var source = Encoding.UTF8.GetBytes("bdgrz.snapshot.source.risk.v1\0{\"events\":[1,2]}");
        var first = new SnapshotSourceContentBlob(Reference([1, 2]), [1, 2]);
        var second = new SnapshotSourceContentBlob(Reference([3, 4]), [3, 4]);
        using var capture = SnapshotSourceCapture.Begin(Uuid.CreateVersion4());
        var digest = Convert.ToHexStringLower(SnapshotSourceCapture.HashData(source));

        // Act
        SnapshotSourceCapture.Risk(digest, source, [first, second, first]);
        Assert.True(capture.TryGet(digest, out var before));
        var expected = File.ReadAllBytes(before!.Path);
        SnapshotSourceCapture.Risk(digest, source, [second, first]);
        Assert.True(capture.TryGet(digest, out var after));

        // Assert
        Assert.Equal(expected, File.ReadAllBytes(after!.Path));
        Assert.Equal(before.Blob, after.Blob);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldRefuseUnverifiedPhysicalEnvelopeGivenMismatchedRiskSourceOrBody(bool wrongSource)
    {
        // Arrange
        var source = Encoding.UTF8.GetBytes("bdgrz.snapshot.source.risk.v1\0{\"events\":[]}");
        byte[] body = [1, 2];
        using var capture = SnapshotSourceCapture.Begin(Uuid.CreateVersion4());
        var digest = Convert.ToHexStringLower(SnapshotSourceCapture.HashData(source));
        var reference = Reference(body);

        // Act
        var failure = Assert.Throws<ImmutableContentIntegrityException>(() => SnapshotSourceCapture.Risk(
            wrongSource ? new string('a', 64) : digest, source,
            [new(reference, wrongSource ? body : [2, 1])]));

        // Assert
        Assert.NotEmpty(failure.Message);
        Assert.True(capture.TryGet(digest, out var preserved));
        Assert.Equal(source, File.ReadAllBytes(preserved!.Path));
    }

    static ImmutableContentRef Reference(byte[] bytes) => new(Uuid.CreateVersion4(),
        Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.LongLength, "application/json", "risk-test.v1");
}
