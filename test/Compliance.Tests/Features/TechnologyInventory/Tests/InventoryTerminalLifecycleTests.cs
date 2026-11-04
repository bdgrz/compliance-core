using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;

public sealed class InventoryTerminalLifecycleTests
{
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
    static readonly DateTimeOffset At = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRetainRetirementWithoutFactsGivenInformationAssetChangeAfterReplay(bool reactivate)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var asset = new InformationAsset(tenant, Uuid.CreateVersion4());
        Assert.True(asset.Record(new InformationAssetContent("Customers", "internal", "RET-1",
            Uuid.CreateVersion4(), null, "active"), Actor, At).IsSuccess);
        Assert.True(asset.Revise(1, value => value with { Lifecycle = "retired" }, Actor, At).IsSuccess);
        var replayed = NativeFacts.Replay(new InformationAsset(tenant, asset.Id), NativeFacts.Raised(asset));
        var before = replayed.Content;

        // Act
        var result = replayed.Revise(2, value => value with
        {
            Name = "Changed", Lifecycle = reactivate ? "active" : "retired"
        }, Actor, At);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before, replayed.Content);
        Assert.Equal(2, replayed.Revision);
        Assert.Empty(new AggregateScenario<InformationAsset>(replayed).PendingEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRetainRetirementWithoutFactsGivenComponentChangeAfterReplay(bool reactivate)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var component = new TechnologyComponent(tenant, Uuid.CreateVersion4());
        Assert.True(component.Record(new TechnologyComponentContent("repository", "API", Uuid.CreateVersion4(),
            "production", "region", null, null, null, "active"), Actor, At).IsSuccess);
        Assert.True(component.Revise(1, value => value with { Lifecycle = "retired" }, Actor, At).IsSuccess);
        var replayed = NativeFacts.Replay(new TechnologyComponent(tenant, component.Id), NativeFacts.Raised(component));
        var before = replayed.Content;

        // Act
        var result = replayed.Revise(2, value => value with
        {
            Name = "Changed", Lifecycle = reactivate ? "active" : "retired"
        }, Actor, At);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before, replayed.Content);
        Assert.Equal(2, replayed.Revision);
        Assert.Empty(new AggregateScenario<TechnologyComponent>(replayed).PendingEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRetainRetirementWithoutFactsGivenFlowChangeAfterReplay(bool reactivate)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var flow = new DataFlow(tenant, Uuid.CreateVersion4());
        Assert.True(flow.Record(new DataFlowContent("technology_component", Uuid.CreateVersion4(),
            "external_party", null, "Payroll", [Uuid.CreateVersion4()], "Export", true, true, null,
            new DateOnly(2026, 11, 4), Uuid.CreateVersion4(), "active", "internal"), Actor, At).IsSuccess);
        Assert.True(flow.Revise(1, value => value with { Lifecycle = "retired" }, Actor, At).IsSuccess);
        var replayed = NativeFacts.Replay(new DataFlow(tenant, flow.Id), NativeFacts.Raised(flow));
        var before = replayed.Content;

        // Act
        var result = replayed.Revise(2, value => value with
        {
            Purpose = "Changed", Lifecycle = reactivate ? "active" : "retired"
        }, Actor, At);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before, replayed.Content);
        Assert.Equal(2, replayed.Revision);
        Assert.Empty(new AggregateScenario<DataFlow>(replayed).PendingEvents);
    }
}
