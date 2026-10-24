using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Retention;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Retention;

public sealed class ArtifactRetentionTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Admin = ActorReference.ForMember(Uuid.CreateVersion4(), "Org Admin");
    static readonly ImmutableContentRef Response = new(Uuid.CreateVersion4(), new string('a', 64), 1, "application/json", "test-response.v1");

    [Fact]
    public void ShouldRecordSevenYearsFromExplicitPeriodGivenSourceBoundImportDecision()
    {
        // Arrange
        var source = Source();
        var id = Uuid.CreateVersion4();
        var basis = new ArtifactRetentionBasis(source.Source, id);
        var operation = Operation(basis, id, "RecordArtifactRetentionBasis", 0);

        // Act
        var result = basis.Record(operation, source, new(2019, 1, 1), new(2019, 12, 31), "Verified", Response, Response);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 12, 31), basis.Basis!.RetainsThrough);
        Assert.Equal(Admin, basis.Basis.RecordedBy);
        Assert.Equal(source.Source, basis.Source);
        Assert.Equal(1, basis.Revision);
        Assert.Single(new AggregateScenario<ArtifactRetentionBasis>(basis).PendingEvents);
    }

    [Fact]
    public void ShouldKeepIndefiniteLegalHoldHistoryGivenAttributedPersonalRelease()
    {
        // Arrange
        var source = Source();
        var holdId = Uuid.CreateVersion4();
        var releaseId = Uuid.CreateVersion4();
        var hold = new ArtifactLegalHold(source.Source.TenantId, holdId, holdId);
        Assert.True(hold.Place(Operation(hold, holdId, "PlaceArtifactLegalHold", 0), source, "Legal matter", Response, Response).IsSuccess);
        var active = hold.View;
        var placed = Assert.IsType<ArtifactLegalHoldPlaced>(Assert.Single(new AggregateScenario<ArtifactLegalHold>(hold).PendingEvents));
        var reopened = new AggregateScenario<ArtifactLegalHold>(new(source.Source.TenantId, holdId, releaseId))
            .Given(placed).Aggregate;

        // Act
        var released = reopened.ReleaseHold(Operation(reopened, releaseId, "ReleaseArtifactLegalHold", 1) with
        { AcceptedAt = Now.AddYears(10) }, source, "Authorized release", Response, Response);

        // Assert
        Assert.True(released.IsSuccess);
        Assert.Null(active!.ReleasedAt);
        Assert.Equal(Admin, reopened.View!.PlacedBy);
        Assert.Equal(Admin, reopened.View.ReleasedBy);
        Assert.Equal(Now.AddYears(10), reopened.View.ReleasedAt);
        Assert.Equal(2, reopened.Revision);
    }

    [Fact]
    public void ShouldRejectRegressingSourcePositionGivenAlreadyBoundAuthority()
    {
        // Arrange
        var source = Source() with { Position = 10 };
        var hold = new ArtifactLegalHold(source.Source.TenantId, Uuid.CreateVersion4());
        var operation = Operation(hold, hold.Id, "PlaceArtifactLegalHold", 0);
        // Select the operation so native replay and changed-intent decisions use the same production protocol.
        hold = new(source.Source.TenantId, hold.Id, hold.Id);
        Assert.True(hold.Place(operation, source, "Preserve", Response, Response).IsSuccess);

        // Act
        var result = hold.PrepareRelease(source with { Position = 9 }, 1, "Release", Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, hold.Revision);
    }

    [Theory]
    [InlineData("digest")]
    [InlineData("tenant")]
    [InlineData("period")]
    [InlineData("revision")]
    [InlineData("reason")]
    public void ShouldRejectChangedIntentGivenRecordedEvidenceBasis(string change)
    {
        // Arrange
        var source = Source("evidence_artifact") with { PeriodStart = new(2019, 1, 1), PeriodEnd = new(2019, 12, 31) };
        var id = Uuid.CreateVersion4();
        var basis = new ArtifactRetentionBasis(source.Source, id);
        var accepted = Operation(basis, id, "RecordArtifactRetentionBasis", 0);
        Assert.True(basis.Record(accepted, source, null, null, "Verified", Response, Response).IsSuccess);
        var changed = accepted with { AuthoredIntentSha256 = new string('b', 64) };
        var altered = change switch
        {
            "digest" => source with { Source = source.Source with { ContentSha256 = new string('b', 64) } },
            "tenant" => source with { Source = source.Source with { TenantId = Uuid.CreateVersion4() } },
            "period" => source with { PeriodEnd = source.PeriodEnd!.Value.AddDays(1) },
            _ => source,
        };
        if (change == "revision")
            changed = changed with { OriginalExpectedRevision = 1 };

        // Act
        var result = basis.Record(changed, altered, null, null, change == "reason" ? "Changed" : "Verified", Response, Response);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, basis.Revision);
        Assert.Equal(source.Source, basis.Source);
    }

    [Fact]
    public void ShouldRemainBlockedGivenElapsedPeriodAndReleasedHold()
    {
        // Arrange
        var source = Source();
        var basisId = Uuid.CreateVersion4();
        var basis = new ArtifactRetentionBasis(source.Source, basisId);
        Assert.True(basis.Record(Operation(basis, basisId, "RecordArtifactRetentionBasis", 0), source,
            new(2019, 1, 1), new(2019, 12, 31), "Verified", Response, Response).IsSuccess);
        var holdId = Uuid.CreateVersion4();
        var hold = new ArtifactLegalHold(source.Source.TenantId, holdId, holdId);
        var placed = Operation(hold, holdId, "PlaceArtifactLegalHold", 0);
        Assert.True(hold.Place(placed, source, "Legal matter", Response, Response).IsSuccess);
        var placement = Assert.IsType<ArtifactLegalHoldPlaced>(Assert.Single(new AggregateScenario<ArtifactLegalHold>(hold).PendingEvents));
        var releaseId = Uuid.CreateVersion4();
        var current = new AggregateScenario<ArtifactLegalHold>(new(source.Source.TenantId, holdId, releaseId))
            .Given(placement).Aggregate;
        Assert.True(current.ReleaseHold(Operation(current, releaseId, "ReleaseArtifactLegalHold", 1), source,
            "Matter closed", Response, Response).IsSuccess);
        var snapshot = new ArtifactRetentionSnapshot(source.Source, basis.Basis, [current.View!], 3, "stable");

        // Act
        var anniversary = snapshot.Assess(new(2026, 12, 31));
        var elapsed = snapshot.Assess(new(2027, 1, 1));

        // Assert
        Assert.False(anniversary.RetentionElapsed);
        Assert.True(elapsed.RetentionElapsed);
        Assert.False(elapsed.DispositionAllowed);
        Assert.Equal(0, elapsed.ActiveLegalHoldCount);
        Assert.Equal(1, elapsed.LegalHoldHistoryCount);
        Assert.Contains("engagement_hold_authority_unavailable", elapsed.Blockers);
        Assert.Contains("historical_reliance_unavailable", elapsed.Blockers);
        Assert.Contains("inline_disposition_unsupported", elapsed.Blockers);
    }

    [Fact]
    public void ShouldRequireExplicitSupportedPeriodGivenRawImportRows()
    {
        // Arrange
        var source = Source();
        var id = Uuid.CreateVersion4();
        var basis = new ArtifactRetentionBasis(source.Source, id);

        // Act
        var result = basis.Record(Operation(basis, id, "RecordArtifactRetentionBasis", 0), source,
            null, null, "Submitted today", Response, Response);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(0, basis.Revision);
        Assert.Empty(new AggregateScenario<ArtifactRetentionBasis>(basis).PendingEvents);
    }

    [Theory]
    [InlineData("basis")]
    [InlineData("place")]
    [InlineData("release")]
    public void ShouldRejectChangedActorGivenAttributedHistoricalDecisionRetry(string kind)
    {
        // Arrange
        var source = Source();
        var id = Uuid.CreateVersion4();
        var basis = new ArtifactRetentionBasis(source.Source, id);
        var hold = new ArtifactLegalHold(source.Source.TenantId, id, id);
        var command = kind == "basis" ? "RecordArtifactRetentionBasis" : "PlaceArtifactLegalHold";
        Aggregate owner = kind == "basis" ? basis : hold;
        var operation = Operation(owner, id, command, 0);
        Assert.True(kind == "basis"
            ? basis.Record(operation, source, new(2019, 1, 1), new(2019, 12, 31), "Verified", Response, Response).IsSuccess
            : hold.Place(operation, source, "Preserve", Response, Response).IsSuccess);
        if (kind == "release")
        {
            var placed = Assert.IsType<ArtifactLegalHoldPlaced>(Assert.Single(new AggregateScenario<ArtifactLegalHold>(hold).PendingEvents));
            id = Uuid.CreateVersion4();
            hold = new AggregateScenario<ArtifactLegalHold>(new(source.Source.TenantId, hold.Id, id))
                .Given(placed).Aggregate;
            operation = Operation(hold, id, "ReleaseArtifactLegalHold", 1);
            Assert.True(hold.ReleaseHold(operation, source, "Released", Response, Response).IsSuccess);
        }
        var changed = operation with { Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Other admin") };

        // Act
        var result = kind switch
        {
            "basis" => basis.Record(changed, source, new(2019, 1, 1), new(2019, 12, 31), "Verified", Response, Response),
            "place" => hold.Place(changed, source, "Preserve", Response, Response),
            _ => hold.ReleaseHold(changed, source, "Released", Response, Response),
        };

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ShouldUseDifferentBasisOwnersGivenDifferentExactContent()
    {
        // Arrange
        var source = Source();

        // Act
        var original = ArtifactRetentionBasis.IdentityOf(source.Source);
        var changed = ArtifactRetentionBasis.IdentityOf(source.Source with { ContentSha256 = new string('b', 64) });

        // Assert
        Assert.NotEqual(original, changed);
    }

    [Theory]
    [InlineData("basis")]
    [InlineData("place")]
    [InlineData("release")]
    public void ShouldFitTheBoundedEventPayloadGivenMaximumEscapedReasonAndActor(string kind)
    {
        // Arrange
        var source = Source();
        var id = Uuid.CreateVersion4();
        var actor = Admin with { Display = new string('\u8c00', 2000) };
        var reason = new string('\u8c00', 2000);
        DomainEvent fact;

        // Act
        if (kind == "basis")
        {
            var basis = new ArtifactRetentionBasis(source.Source, id);
            Assert.True(basis.Record(Operation(basis, id, "RecordArtifactRetentionBasis", 0) with { Actor = actor },
                source, new(2019, 1, 1), new(2019, 12, 31), reason, Response, Response).IsSuccess);
            fact = Assert.Single(new AggregateScenario<ArtifactRetentionBasis>(basis).PendingEvents);
        }
        else
        {
            var hold = new ArtifactLegalHold(source.Source.TenantId, id, id);
            Assert.True(hold.Place(Operation(hold, id, "PlaceArtifactLegalHold", 0) with { Actor = actor },
                source, reason, Response, Response).IsSuccess);
            var placed = Assert.IsType<ArtifactLegalHoldPlaced>(Assert.Single(new AggregateScenario<ArtifactLegalHold>(hold).PendingEvents));
            if (kind == "release")
            {
                var releaseId = Uuid.CreateVersion4();
                hold = new AggregateScenario<ArtifactLegalHold>(new(source.Source.TenantId, id, releaseId))
                    .Given(placed).Aggregate;
                Assert.True(hold.ReleaseHold(Operation(hold, releaseId, "ReleaseArtifactLegalHold", 1) with { Actor = actor },
                    source, reason, Response, Response).IsSuccess);
                fact = Assert.Single(new AggregateScenario<ArtifactLegalHold>(hold).PendingEvents);
            }
            else
                fact = placed;
        }
        var bytes = fact switch
        {
            ArtifactRetentionBasisRecorded basis => System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(basis,
                Bdgrz.Compliance.ComplianceCoreJsonContext.Default.ArtifactRetentionBasisRecorded),
            ArtifactLegalHoldPlaced placed => System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(placed,
                Bdgrz.Compliance.ComplianceCoreJsonContext.Default.ArtifactLegalHoldPlaced),
            ArtifactLegalHoldReleased released => System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(released,
                Bdgrz.Compliance.ComplianceCoreJsonContext.Default.ArtifactLegalHoldReleased),
            _ => throw new InvalidOperationException("The retention operation must emit its bounded native fact."),
        };

        // Assert
        Assert.InRange(bytes.Length, 1, 48 * 1024);
    }

    static ArtifactRetentionSourceSnapshot Source(string kind = "application_import") =>
        new(new(Uuid.CreateVersion4(), kind, Uuid.CreateVersion4(), new string('a', 64)), 1);

    static OperationProvenance Operation(Aggregate owner, Uuid id, string kind, ulong revision) =>
        new(id, id, null, kind, Admin, owner.Stream, "initial", revision,
            ContentDigest.Sha256(System.Text.Encoding.UTF8.GetBytes(kind)), Now, owner.Stream, owner.Stream);
}
