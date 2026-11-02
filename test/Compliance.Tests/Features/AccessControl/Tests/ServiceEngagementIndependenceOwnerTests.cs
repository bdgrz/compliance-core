using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

/// <summary>Direct owner guard behavior for engagement lifecycle changes.</summary>
public sealed class ServiceEngagementIndependenceOwnerTests
{
    [Fact]
    public void ShouldRefuseCreationWithoutFactGivenAnotherOperationIdentity()
    {
        // Arrange
        var owner = new ServiceEngagementIndependence(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var scenario = new AggregateScenario<ServiceEngagementIndependence>(owner);
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), ServiceEngagementIndependence.CreateKind, 0);
        var content = NativeFacts.Ref();

        // Act
        var result = owner.Change(ServiceEngagementIndependence.CreateKind, operation, content, content, content);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.False(owner.Exists);
    }

    [Fact]
    public void ShouldRefuseUnknownLifecycleKindGivenFreshEngagement()
    {
        // Arrange
        var owner = new ServiceEngagementIndependence(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var scenario = new AggregateScenario<ServiceEngagementIndependence>(owner);

        // Act
        var result = owner.PrepareChange("unknown", 0);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldCreateOneFactGivenValidRootProvenance()
    {
        // Arrange
        var owner = new ServiceEngagementIndependence(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var scenario = new AggregateScenario<ServiceEngagementIndependence>(owner);
        var operation = NativeFacts.Root(owner, owner.EngagementId, ServiceEngagementIndependence.CreateKind, 0);
        var content = NativeFacts.Ref();

        // Act
        var result = owner.Change(ServiceEngagementIndependence.CreateKind, operation, content, content, content);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(scenario.PendingEvents);
        Assert.True(owner.Exists);
        Assert.Equal(1UL, owner.Revision);
    }

    [Theory]
    [InlineData("affected_owner")]
    [InlineData("revision_owner")]
    [InlineData("first_owner")]
    [InlineData("causation")]
    [InlineData("episode")]
    [InlineData("digest")]
    [InlineData("accepted_at")]
    public void ShouldRefuseCreationWithoutFactGivenInvalidRootProvenance(string fault)
    {
        // Arrange
        var owner = new ServiceEngagementIndependence(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var scenario = new AggregateScenario<ServiceEngagementIndependence>(owner);
        var foreign = new ServiceEngagementIndependence(Uuid.CreateVersion4(), Uuid.CreateVersion4()).Stream;
        var root = NativeFacts.Root(owner, owner.EngagementId, ServiceEngagementIndependence.CreateKind, 0);
        var operation = fault switch
        {
            "affected_owner" => root with { AffectedOwner = foreign },
            "revision_owner" => root with { ExpectedRevisionOwner = foreign },
            "first_owner" => root with { FirstOwner = foreign },
            "causation" => root with { CausationEventId = Uuid.CreateVersion4() },
            "episode" => root with { Episode = "other" },
            "digest" => root with { AuthoredIntentSha256 = "invalid" },
            "accepted_at" => root with { AcceptedAt = default },
            _ => throw new ArgumentOutOfRangeException(nameof(fault)),
        };
        var content = NativeFacts.Ref();

        // Act
        var result = owner.Change(ServiceEngagementIndependence.CreateKind, operation, content, content, content);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.False(owner.Exists);
    }
}
