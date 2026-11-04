using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;

public sealed class InformationAssetNamedChangeTests
{
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
    static readonly DateTimeOffset At = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRecordOnlyClassificationGivenNamedClassificationChange()
    {
        // Arrange
        var asset = Recorded();
        var original = asset.Content!;
        var operation = Operation(asset, "ClassifyInformationAsset", 1);

        // Act
        var result = asset.Classify(operation, NativeFacts.Ref(), 1, " confidential ", Actor, At);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var fact = Assert.IsType<InformationAssetClassified>(new AggregateScenario<InformationAsset>(asset).PendingEvents.Last());
        Assert.Equal("internal", fact.PreviousClassification);
        Assert.Equal("confidential", fact.Classification);
        Assert.Equal(original with { Classification = "confidential" }, asset.Content);
        Assert.Equal(2, asset.Revision);
    }

    [Theory]
    [InlineData("internal", 1)]
    [InlineData("secret", 1)]
    [InlineData("restricted", 0)]
    public void ShouldAppendNothingGivenInvalidOrUnchangedClassification(string classification, long expected)
    {
        // Arrange
        var asset = Recorded();
        var replayed = NativeFacts.Replay(new InformationAsset(Tenant(asset), asset.Id), NativeFacts.Raised(asset));

        // Act
        var result = replayed.Classify(Operation(replayed, "ClassifyInformationAsset", expected),
            NativeFacts.Ref(), expected, classification, Actor, At);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("internal", replayed.Content!.Classification);
        Assert.Equal(1, replayed.Revision);
        Assert.Empty(new AggregateScenario<InformationAsset>(replayed).PendingEvents);
    }

    [Fact]
    public void ShouldKeepRetirementReasonAndRejectLaterChangeGivenNamedRetirementReplay()
    {
        // Arrange
        var asset = Recorded();
        var retirement = Operation(asset, "RetireInformationAsset", 1);
        Assert.False(asset.Retire(retirement, NativeFacts.Ref(), 1, " ", At, Actor, At).IsSuccess);
        Assert.True(asset.Retire(retirement, NativeFacts.Ref(), 1, "No longer held", At, Actor, At).IsSuccess);
        var fact = Assert.IsType<InformationAssetRetired>(new AggregateScenario<InformationAsset>(asset).PendingEvents.Last());
        var replayed = NativeFacts.Replay(new InformationAsset(Tenant(asset), asset.Id), NativeFacts.Raised(asset));

        // Act
        var result = replayed.ChangeDetails(Operation(replayed, "ChangeInformationAssetDetails", 2), NativeFacts.Ref(),
            2, "Changed", "RET-2", "Later", Actor, At);

        // Assert
        Assert.Equal("No longer held", fact.Reason);
        Assert.Equal(At, fact.EffectiveAt);
        Assert.Equal("retired", replayed.Content!.Lifecycle);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<InformationAsset>(replayed).PendingEvents);
    }

    [Fact]
    public void ShouldReplayOriginalClassificationAcceptanceGivenLaterDetailsChange()
    {
        // Arrange
        var asset = Recorded();
        var operation = Operation(asset, "ClassifyInformationAsset", 1);
        Assert.True(asset.Classify(operation, NativeFacts.Ref(), 1, "restricted", Actor, At).IsSuccess);
        Assert.True(asset.ChangeDetails(Operation(asset, "ChangeInformationAssetDetails", 2), NativeFacts.Ref(),
            2, "Renamed", "RET-2", null, Actor, At).IsSuccess);
        var replayed = NativeFacts.Replay(new InformationAsset(Tenant(asset), asset.Id, operation.LogicalId),
            NativeFacts.Raised(asset));

        // Act
        var retry = replayed.Acceptance.Decide(AcceptedOperationBinding.Of(operation));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, retry);
        Assert.IsType<InformationAssetClassified>(replayed.Acceptance.SelectedFact);
        Assert.Equal(3, replayed.Revision);
        Assert.Equal("restricted", replayed.Content!.Classification);
    }

    static InformationAsset Recorded()
    {
        var asset = new InformationAsset(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(asset.Record(Operation(asset, "RecordInformationAsset", null), NativeFacts.Ref(),
            new InformationAssetContent("Customers", "internal", "RET-1", Uuid.CreateVersion4(), null, "active"), Actor, At).IsSuccess);
        return asset;
    }

    static Uuid Tenant(InformationAsset asset) => Uuid.Parse(asset.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture);

    static OperationProvenance Operation(InformationAsset asset, string kind, long? expected)
    {
        var id = Uuid.CreateVersion4();
        return new OperationProvenance(id, id, null, kind, Actor, asset.Stream,
            expected is null ? "initial" : "current", expected is null ? null : (ulong)expected,
            new string('a', 64), At, asset.Stream, asset.Stream);
    }
}
