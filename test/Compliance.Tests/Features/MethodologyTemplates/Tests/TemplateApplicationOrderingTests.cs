using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class TemplateApplicationOrderingTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly Uuid Template = Uuid.CreateVersion4();
    static readonly Uuid Actor = Uuid.CreateVersion4();
    static readonly ImmutableContentRef Manifest = new(Uuid.CreateVersion4(), new string('a', 64), 20,
        "application/json", MethodologyTemplateDraft.EntriesSchema);

    [Fact]
    public void ShouldRefuseNextVersionWithoutFactGivenPreviousVersionIncomplete()
    {
        // Arrange
        var scenario = Scenario();
        Assert.True(Apply(scenario.Aggregate, Uuid.CreateVersion4(), 1).IsSuccess);
        var before = scenario.PendingEvents.Count;

        // Act
        var result = Apply(scenario.Aggregate, Uuid.CreateVersion4(), 2);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.Equal(1, scenario.Aggregate.LastAppliedVersion);
        Assert.Equal(before, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldApplyIncreasingVersionsGivenMatchingCompletionAndKeepOldRetryFactFree()
    {
        // Arrange
        var scenario = Scenario();
        var first = Uuid.CreateVersion4();
        Assert.True(Apply(scenario.Aggregate, first, 1).IsSuccess);
        Assert.True(scenario.Aggregate.CompleteVersion(first, 1, new(1, 0, 0), Uuid.CreateVersion4()).IsSuccess);

        // Act
        var next = Apply(scenario.Aggregate, Uuid.CreateVersion4(), 3);
        var beforeRetry = scenario.PendingEvents.Count;
        var oldRetry = Apply(scenario.Aggregate, first, 1);
        var changedRetry = Apply(scenario.Aggregate, first, 2);

        // Assert
        Assert.True(next.IsSuccess);
        Assert.True(oldRetry.IsSuccess);
        Assert.False(changedRetry.IsSuccess);
        Assert.Equal(3, scenario.Aggregate.LastAppliedVersion);
        Assert.False(scenario.Aggregate.IsVersionCompleted);
        Assert.Equal(beforeRetry, scenario.PendingEvents.Count);
    }

    [Theory]
    [InlineData(-1, 1, 1)]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 0)]
    public void ShouldRefuseInvalidTotalsWithoutFactGivenOnePlannedEntry(int created, int proposed, int refused)
    {
        // Arrange
        var scenario = Scenario();
        var command = Uuid.CreateVersion4();
        Assert.True(Apply(scenario.Aggregate, command, 1).IsSuccess);
        var before = scenario.PendingEvents.Count;

        // Act
        var result = scenario.Aggregate.CompleteVersion(command, 1,
            new(created, proposed, refused), Uuid.CreateVersion4());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(scenario.Aggregate.IsVersionCompleted);
        Assert.Equal(before, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseWrongCompletionAndChangedRetryGivenExactVersionBinding()
    {
        // Arrange
        var scenario = Scenario();
        var command = Uuid.CreateVersion4();
        var completion = Uuid.CreateVersion4();
        Assert.True(Apply(scenario.Aggregate, command, 1).IsSuccess);
        var before = scenario.PendingEvents.Count;

        // Act
        var wrong = scenario.Aggregate.CompleteVersion(Uuid.CreateVersion4(), 1, new(1, 0, 0), completion);
        var accepted = scenario.Aggregate.CompleteVersion(command, 1, new(1, 0, 0), completion);
        var retry = scenario.Aggregate.CompleteVersion(command, 1, new(1, 0, 0), completion);
        var changed = scenario.Aggregate.CompleteVersion(command, 1, new(0, 1, 0), completion);

        // Assert
        Assert.False(wrong.IsSuccess);
        Assert.True(accepted.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(before + 1, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldReplayOrderingAndCompletionGivenRetainedFacts()
    {
        // Arrange
        var scenario = Scenario();
        var command = Uuid.CreateVersion4();
        Assert.True(Apply(scenario.Aggregate, command, 1).IsSuccess);
        Assert.True(scenario.Aggregate.CompleteVersion(command, 1, new(1, 0, 0), Uuid.CreateVersion4()).IsSuccess);
        var replay = Scenario().Given(scenario.PendingEvents.ToArray());

        // Act
        var result = Apply(replay.Aggregate, Uuid.CreateVersion4(), 1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(replay.Aggregate.IsVersionCompleted);
        Assert.Equal(1, replay.Aggregate.LastAppliedVersion);
        Assert.Empty(replay.PendingEvents);
    }

    [Fact]
    public void ShouldDeriveOneTenantProgramTemplateIdentityGivenCanonicalApplication()
    {
        // Arrange
        var expected = TemplateApplication.CanonicalIdentity(Tenant, Program, Template);

        // Act
        var owner = new TemplateApplication(Tenant, Program, Template, true);
        var other = TemplateApplication.CanonicalIdentity(Uuid.CreateVersion4(), Program, Template);

        // Assert
        Assert.Equal(expected, owner.Id);
        Assert.NotEqual(expected, other);
    }

    static AggregateScenario<TemplateApplication> Scenario() => new(new TemplateApplication(Tenant, Program, Template, true));

    static Result Apply(TemplateApplication owner, Uuid command, long version) => owner.ApplyVersion(command,
        version, Actor, new NativeFactReference(new MethodologyTemplate(Template).Stream,
            Uuid.CreateVersion5(Template, "publication:" + version), (ulong)version), 0,
        Manifest, ["control"], []);
}
