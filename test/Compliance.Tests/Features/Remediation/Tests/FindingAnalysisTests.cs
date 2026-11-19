using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class FindingAnalysisTests
{
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Analyst");

    [Fact]
    public void ShouldRetainSourceAndOwnershipGivenNamedAnalysisRevision()
    {
        // Arrange
        var owner = Raised();
        var source = owner.Raised!.Source;
        var originalOwner = owner.OwnerMemberId;
        var affected = new[] { new FindingLink("control", Uuid.CreateVersion4().ToString()) };

        // Act
        var result = owner.ReviseAnalysis(Operation(owner, Uuid.CreateVersion4(), nameof(ReviseFindingAnalysis), 1),
            "  Leaver reconciliation missed the account.  ", "moderate", affected, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Leaver reconciliation missed the account.", owner.Analysis);
        Assert.Equal("moderate", owner.Severity);
        Assert.Equal(affected, owner.AffectedRecords);
        Assert.Equal(source, owner.Raised.Source);
        Assert.Equal(originalOwner, owner.OwnerMemberId);
        Assert.Equal(2, owner.Revision);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ShouldRefuseWithoutFactGivenEmptyAnalysis(string analysis)
    {
        // Arrange
        var owner = Raised();
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = owner.ReviseAnalysis(Operation(owner, Uuid.CreateVersion4(), nameof(ReviseFindingAnalysis), 1),
            analysis, "moderate", [], NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(1, owner.Revision);
        Assert.Null(owner.Analysis);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenUnchangedAnalysisIntent()
    {
        // Arrange
        var owner = Raised();
        Assert.True(owner.ReviseAnalysis(Operation(owner, Uuid.CreateVersion4(), nameof(ReviseFindingAnalysis), 1),
            "Reviewed cause.", "low", [], NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = owner.ReviseAnalysis(Operation(owner, Uuid.CreateVersion4(), nameof(ReviseFindingAnalysis), 2),
            "Reviewed cause.", "low", [], NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(2, owner.Revision);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseCompetingAppendGivenConsumedAnalysisRevision()
    {
        // Arrange
        var owner = Raised();
        Assert.True(owner.ReviseAnalysis(Operation(owner, Uuid.CreateVersion4(), nameof(ReviseFindingAnalysis), 1),
            "First analysis.", "low", [], NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = owner.ReviseAnalysis(Operation(owner, Uuid.CreateVersion4(), nameof(ReviseFindingAnalysis), 1),
            "Competing analysis.", "high", [], NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("First analysis.", owner.Analysis);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    static FindingAggregate Raised()
    {
        var owner = new FindingAggregate(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(owner.Raise(Operation(owner, owner.FindingId, nameof(RaiseFinding), null),
            new FindingRaiseTerms(new FindingSource("manual", null, null, "Original source wording."), "Leaver account",
                "Original description.", "high", "VPN", Uuid.CreateVersion4(), new DateOnly(2026, 12, 1),
                Actor, NativeFacts.Now, "manual", null), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref(), null).IsSuccess);
        return owner;
    }

    static Bdgrz.Compliance.Features.NativeOperations.Models.OperationProvenance Operation(
        FindingAggregate owner, Uuid id, string kind, ulong? expected) =>
        FindingContent.Provenance(id, kind, owner.Stream, Actor, expected, new string('a', 64), NativeFacts.Now) with
        { FirstOwner = owner.Stream };
}
