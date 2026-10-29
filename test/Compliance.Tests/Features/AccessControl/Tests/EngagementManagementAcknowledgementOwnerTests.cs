using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class EngagementManagementAcknowledgementOwnerTests
{
    [Fact]
    public void ShouldRefuseMaterializationWithoutFactGivenDifferentRootOrOwner()
    {
        // Arrange
        var owner = Fresh(out var scenario);
        var operation = Child(owner);
        var wrongRoot = operation with { RootOperationId = Uuid.CreateVersion4() };
        var foreignOwner = operation with
        {
            Owner = new EngagementManagementAcknowledgement(Uuid.CreateVersion4(), Uuid.CreateVersion4()).Stream,
        };

        // Act
        var wrongRootResult = owner.Materialize(wrongRoot, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
        var foreignResult = owner.Materialize(foreignOwner, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.False(wrongRootResult.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, wrongRootResult.Error!.Kind);
        Assert.False(foreignResult.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, foreignResult.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.Null(owner.Acknowledged);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("acknowledgement")]
    [InlineData("logical")]
    [InlineData("cause")]
    [InlineData("kind")]
    [InlineData("source")]
    [InlineData("invalid_source")]
    [InlineData("empty_source")]
    [InlineData("affected")]
    [InlineData("revision_owner")]
    public void ShouldRefuseMaterializationWithoutFactGivenInvalidIdentityOrChildProvenance(string defect)
    {
        // Arrange
        var tenant = defect == "tenant" ? Uuid.Empty : Uuid.CreateVersion4();
        var acknowledgement = defect == "acknowledgement" ? Uuid.Empty : Uuid.CreateVersion4();
        var owner = new EngagementManagementAcknowledgement(tenant, acknowledgement);
        var scenario = new AggregateScenario<EngagementManagementAcknowledgement>(owner);
        var operation = Child(owner);
        var foreign = new ServiceEngagementIndependence(Uuid.CreateVersion4(), Uuid.CreateVersion4()).Stream;
        operation = defect switch
        {
            "tenant" or "acknowledgement" => operation,
            "logical" => operation with { LogicalId = Uuid.Empty },
            "cause" => operation with { CausationEventId = null },
            "kind" => operation with { CommandKind = "Other" },
            "source" => operation with { FirstOwner = foreign },
            "invalid_source" => operation with
            {
                FirstOwner = new EventStreamAddress(owner.Stream.Realm, ServiceEngagementIndependence.Area,
                    "not-an-engagement"),
            },
            "empty_source" => operation with
            {
                FirstOwner = new ServiceEngagementIndependence(tenant, Uuid.Empty).Stream,
            },
            "affected" => operation with { AffectedOwner = foreign },
            "revision_owner" => operation with { ExpectedRevisionOwner = foreign },
            _ => throw new ArgumentOutOfRangeException(nameof(defect)),
        };

        // Act
        var result = owner.Materialize(operation, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.Null(owner.Acknowledged);
    }

    [Fact]
    public void ShouldPreserveOriginalAcknowledgementAndRefuseAnotherGivenValidReplay()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var acknowledgement = Uuid.CreateVersion4();
        var seed = new EngagementManagementAcknowledgement(tenant, acknowledgement);
        var operation = Child(seed);
        var content = NativeFacts.Ref();
        var intent = NativeFacts.Ref();
        Assert.True(seed.Materialize(operation, content, intent, NativeFacts.Ref()).IsSuccess);
        var facts = NativeFacts.Raised(seed);
        var owner = NativeFacts.Replay(new EngagementManagementAcknowledgement(tenant, acknowledgement), facts);
        var scenario = new AggregateScenario<EngagementManagementAcknowledgement>(owner);

        // Act
        var another = owner.Materialize(Child(owner), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.False(another.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, another.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        var fact = Assert.IsType<EngagementManagementAcknowledged>(Assert.Single(facts));
        Assert.Equal(content, owner.Acknowledged!.AcknowledgementContent);
        Assert.Equal(intent, owner.Acknowledged.Intent);
        Assert.Equal(acknowledgement, fact.Operation.RootOperationId);
    }

    [Fact]
    public void ShouldKeepIdentityStableGivenTenantAndAcknowledgement()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var acknowledgement = Uuid.CreateVersion4();

        // Act
        var first = new EngagementManagementAcknowledgement(tenant, acknowledgement);
        var selected = new EngagementManagementAcknowledgement(tenant, acknowledgement, Uuid.CreateVersion4());
        var otherTenant = new EngagementManagementAcknowledgement(Uuid.CreateVersion4(), acknowledgement);
        var otherAcknowledgement = new EngagementManagementAcknowledgement(tenant, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(first.Stream, selected.Stream);
        Assert.Equal(first.Id, selected.Id);
        Assert.NotEqual(first.Stream, otherTenant.Stream);
        Assert.NotEqual(first.Stream, otherAcknowledgement.Stream);
    }

    static EngagementManagementAcknowledgement Fresh(
        out AggregateScenario<EngagementManagementAcknowledgement> scenario)
    {
        var owner = new EngagementManagementAcknowledgement(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        scenario = new AggregateScenario<EngagementManagementAcknowledgement>(owner);
        return owner;
    }

    static OperationProvenance Child(EngagementManagementAcknowledgement owner) =>
        NativeFacts.Root(owner, owner.AcknowledgementId, nameof(MaterializeEngagementManagementAcknowledgement), 0)
            with
        {
            LogicalId = Uuid.CreateVersion4(),
            CausationEventId = Uuid.CreateVersion4(),
            FirstOwner = new ServiceEngagementIndependence(owner.TenantId, Uuid.CreateVersion4()).Stream,
        };
}
