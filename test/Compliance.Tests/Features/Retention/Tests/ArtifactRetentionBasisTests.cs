using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Projections;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Retention.Tests;

public sealed class ArtifactRetentionBasisTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Admin = ActorReference.ForMember(Uuid.CreateVersion4(), "Org Admin");
    static readonly ImmutableContentRef Response = new(Uuid.CreateVersion4(), new string('a', 64), 1, "application/json", "test-response.v1");

    [Fact]
    public void ShouldAcknowledgeExactRetryWithoutNewFactGivenRecordedBasis()
    {
        // Arrange
        var source = Source();
        var id = Uuid.CreateVersion4();
        var basis = new ArtifactRetentionBasis(source.Source, id);
        var operation = Operation(basis, id, 0);
        Assert.True(basis.Record(operation, source, new(2019, 1, 1), new(2019, 12, 31), "Verified", Response, Response).IsSuccess);
        var before = NativeFacts.Raised(basis).Length;

        // Act
        var retry = basis.Record(operation, source, new(2019, 1, 1), new(2019, 12, 31), "Verified", Response, Response);
        var after = NativeFacts.Raised(basis).Length;

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(1, before);
        Assert.Equal(before, after);
        Assert.Equal(1, basis.Revision);
    }

    [Fact]
    public void ShouldRefuseSecondDecisionGivenRecordedBasisAndDifferentOperation()
    {
        // Arrange
        var source = Source();
        var id = Uuid.CreateVersion4();
        var seed = new ArtifactRetentionBasis(source.Source, id);
        Assert.True(seed.Record(Operation(seed, id, 0), source, new(2019, 1, 1), new(2019, 12, 31), "Verified", Response, Response).IsSuccess);
        var history = NativeFacts.Raised(seed);
        var contender = Uuid.CreateVersion4();
        var basis = NativeFacts.Replay(new ArtifactRetentionBasis(source.Source, contender), history);

        // Act
        var stale = basis.Record(Operation(basis, contender, 0), source, new(2020, 1, 1), new(2020, 12, 31), "Changed", Response, Response);
        var current = basis.Record(Operation(basis, contender, 1), source, new(2020, 1, 1), new(2020, 12, 31), "Changed", Response, Response);

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.False(current.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, current.Error!.Kind);
        Assert.Equal(new DateOnly(2019, 12, 31), basis.Basis!.PeriodEnd);
        Assert.Empty(NativeFacts.Raised(basis));
        Assert.Single(history);
    }

    [Fact]
    public void ShouldDeriveSourcePeriodBasisGivenEvidenceArtifactWithoutExplicitPeriod()
    {
        // Arrange
        var source = Source("evidence_artifact") with { PeriodStart = new(2019, 1, 1), PeriodEnd = new(2019, 12, 31) };
        var id = Uuid.CreateVersion4();
        var basis = new ArtifactRetentionBasis(source.Source, id);

        // Act
        var result = basis.Record(Operation(basis, id, 0), source, null, null, "  Verified  ", Response, Response);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("source_period", basis.Basis!.BasisKind);
        Assert.Equal(new DateOnly(2019, 1, 1), basis.Basis.PeriodStart);
        Assert.Equal(new DateOnly(2026, 12, 31), basis.Basis.RetainsThrough);
        Assert.Equal("Verified", basis.Basis.Reason);
    }

    [Fact]
    public void ShouldRecordAdminPeriodBasisGivenApplicationImportExplicitPeriod()
    {
        // Arrange
        var source = Source();
        var id = Uuid.CreateVersion4();
        var basis = new ArtifactRetentionBasis(source.Source, id);

        // Act
        var result = basis.Record(Operation(basis, id, 0), source, new(2020, 2, 1), new(2020, 2, 29), "Verified", Response, Response);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("admin_period", basis.Basis!.BasisKind);
        Assert.Equal(new DateOnly(2027, 2, 28), basis.Basis.RetainsThrough);
    }

    [Theory]
    [InlineData("mismatched-start")]
    [InlineData("mismatched-end")]
    [InlineData("native-period-missing")]
    public void ShouldAppendNothingGivenEvidencePeriodDisagreeingWithSource(string change)
    {
        // Arrange
        var native = Source("evidence_artifact") with { PeriodStart = new(2019, 1, 1), PeriodEnd = new(2019, 12, 31) };
        var source = change == "native-period-missing" ? native with { PeriodStart = null, PeriodEnd = null } : native;
        var id = Uuid.CreateVersion4();
        var basis = new ArtifactRetentionBasis(source.Source, id);
        DateOnly? start = change == "mismatched-start" ? new DateOnly(2019, 1, 2) : null;
        DateOnly? end = change == "mismatched-end" ? new DateOnly(2020, 12, 31) : null;
        if (change == "mismatched-start")
            end = new DateOnly(2019, 12, 31);
        if (change == "mismatched-end")
            start = new DateOnly(2019, 1, 1);

        // Act
        var result = basis.Record(Operation(basis, id, 0), source, start, end, "Verified", Response, Response);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(0, basis.Revision);
        Assert.Empty(NativeFacts.Raised(basis));
    }

    [Theory]
    [InlineData("stale-revision")]
    [InlineData("source-position-zero")]
    [InlineData("other-content")]
    [InlineData("one-sided-period")]
    [InlineData("reversed-period")]
    [InlineData("period-beyond-policy-bound")]
    [InlineData("blank-reason")]
    [InlineData("oversized-reason")]
    [InlineData("foreign-owner-operation")]
    [InlineData("wrong-command-kind")]
    public void ShouldAppendNothingGivenInvalidDecision(string change)
    {
        // Arrange
        var source = Source();
        var id = Uuid.CreateVersion4();
        var basis = new ArtifactRetentionBasis(source.Source, id);
        var operation = Operation(basis, id, change == "stale-revision" ? 1UL : 0UL);
        var supplied = change switch
        {
            "source-position-zero" => source with { Position = 0 },
            "other-content" => source with { Source = source.Source with { ContentSha256 = new string('b', 64) } },
            _ => source,
        };
        DateOnly? start = new DateOnly(2019, 1, 1);
        DateOnly? end = new DateOnly(2019, 12, 31);
        var reason = "Verified";
        switch (change)
        {
            case "one-sided-period":
                end = null;
                break;
            case "reversed-period":
                start = new DateOnly(2020, 1, 1);
                break;
            case "period-beyond-policy-bound":
                start = new DateOnly(9993, 1, 1);
                end = new DateOnly(9993, 12, 31);
                break;
            case "blank-reason":
                reason = "   ";
                break;
            case "oversized-reason":
                reason = new string('x', 2001);
                break;
            case "foreign-owner-operation":
                operation = operation with { Owner = new EventStreamAddress(Uuid.CreateVersion4().ToString(), "artifact-retention-bases", id.ToString()) };
                break;
            case "wrong-command-kind":
                operation = operation with { CommandKind = "PlaceArtifactLegalHold" };
                break;
        }

        // Act
        var result = basis.Record(operation, supplied, start, end, reason, Response, Response);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(0, basis.Revision);
        Assert.Null(basis.Basis);
        Assert.Empty(NativeFacts.Raised(basis));
    }

    [Fact]
    public void ShouldRestoreBasisAndAcceptRetryGivenValidHistoryReplay()
    {
        // Arrange
        var source = Source();
        var id = Uuid.CreateVersion4();
        var seed = new ArtifactRetentionBasis(source.Source, id);
        var operation = Operation(seed, id, 0);
        Assert.True(seed.Record(operation, source, new(2019, 1, 1), new(2019, 12, 31), "Verified", Response, Response).IsSuccess);
        var history = NativeFacts.Raised(seed);

        // Act
        var restored = NativeFacts.Replay(new ArtifactRetentionBasis(source.Source, id), history);
        var revision = restored.Revision;
        var retainsThrough = restored.Basis!.RetainsThrough;
        var retry = restored.Record(operation, source, new(2019, 1, 1), new(2019, 12, 31), "Verified", Response, Response);
        var changed = restored.Record(operation with { AuthoredIntentSha256 = new string('b', 64) }, source,
            new(2019, 1, 1), new(2019, 12, 31), "Verified", Response, Response);

        // Assert
        Assert.Single(history);
        Assert.Equal(1, revision);
        Assert.Equal(new DateOnly(2026, 12, 31), retainsThrough);
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(1, restored.Revision);
    }

    [Fact]
    public void ShouldKeepStreamIdentityGivenDifferentSelectedOperations()
    {
        // Arrange
        var source = Source();

        // Act
        var first = new ArtifactRetentionBasis(source.Source, Uuid.CreateVersion4());
        var second = new ArtifactRetentionBasis(source.Source);

        // Assert
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Stream, second.Stream);
        Assert.Equal(ArtifactRetentionBasis.IdentityOf(source.Source), first.Id);
    }

    [Theory]
    [InlineData("unsupported-kind")]
    [InlineData("empty-source")]
    [InlineData("empty-tenant")]
    [InlineData("short-digest")]
    [InlineData("uppercase-digest")]
    public void ShouldRefuseIdentityGivenInexactSourceContent(string change)
    {
        // Arrange
        var source = Source().Source;
        var inexact = change switch
        {
            "unsupported-kind" => source with { SourceKind = "client_export" },
            "empty-source" => source with { SourceId = Uuid.Empty },
            "empty-tenant" => source with { TenantId = Uuid.Empty },
            "short-digest" => source with { ContentSha256 = new string('a', 63) },
            _ => source with { ContentSha256 = new string('A', 64) },
        };

        // Act
        var exception = Record.Exception(() => ArtifactRetentionBasis.IdentityOf(inexact));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    static ArtifactRetentionSourceSnapshot Source(string kind = "application_import") =>
        new(new(Uuid.CreateVersion4(), kind, Uuid.CreateVersion4(), new string('a', 64)), 1);

    static OperationProvenance Operation(Aggregate owner, Uuid id, ulong revision) =>
        new(id, id, null, "RecordArtifactRetentionBasis", Admin, owner.Stream, "initial", revision,
            ContentDigest.Sha256(System.Text.Encoding.UTF8.GetBytes("RecordArtifactRetentionBasis")), Now, owner.Stream, owner.Stream);
}
