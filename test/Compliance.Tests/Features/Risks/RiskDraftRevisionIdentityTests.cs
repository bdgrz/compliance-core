using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskDraftRevisionIdentityTests
{
    [Theory]
    [InlineData("actor")]
    [InlineData("revision")]
    [InlineData("content")]
    public void ShouldKeepOriginalRevisionBindingGivenLaterRevisionAndDurableReplay(string change)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var id = RiskDraft.IdFor(tenant, program, "R-01");
        var command = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var owner = new RiskDraft(tenant, id, command);
        Assert.True(owner.Create(program, Uuid.CreateVersion4(), "R-01", new("Title", "Scenario", "Effect", null), actor, "Author", now).IsSuccess);
        var request = new ReviseRiskDraft(command, tenant, program, id, 1, "Revised title", "Scenario", "Effect");
        Assert.Null(owner.Revise(request, actor, "Author", now));
        Assert.Null(owner.Revise(program, 2, new("Later title", "Later scenario", "Effect", null), actor, "Author", now));
        var facts = new AggregateScenario<RiskDraft>(owner).PendingEvents.ToArray();
        var hydrated = new AggregateScenario<RiskDraft>(new(tenant, id, command)).Given(facts);

        // Act
        var original = hydrated.Aggregate.Revise(request, actor, "Updated display", now.AddDays(1));
        var changed = hydrated.Aggregate.Revise(request with
        {
            ExpectedRevision = change == "revision" ? 2 : request.ExpectedRevision,
            Title = change == "content" ? "Changed original intent" : request.Title,
        }, change == "actor" ? Uuid.CreateVersion4() : actor, "Author", now.AddDays(1));

        // Assert
        Assert.Null(original);
        Assert.Equal(CommandFailureCode.StateConflict, changed?.Code);
        Assert.Equal(3, hydrated.Aggregate.Revision);
        Assert.Empty(hydrated.PendingEvents);
    }
}
