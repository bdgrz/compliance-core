using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Handlers;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskDraftRevisionIdentityTests
{
    [Theory]
    [InlineData("actor")]
    [InlineData("revision")]
    [InlineData("content")]
    public async Task ShouldKeepOriginalRevisionBindingGivenLaterRevisionAndDurableReplay(string change)
    {
        // Arrange
        await using var fixture = new RiskDraftSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var id = RiskDraft.IdFor(tenant, program, "R-01");
        var command = Uuid.CreateVersion4();
        await fixture.SeedProgramAsync(tenant, program, RbacIds.Member(tenant, user));
        var creation = new CreateRiskDraft(id, tenant, program, "R-01", "Title", "Scenario", "Effect");
        Assert.True((await new CreateRiskDraftHandler(fixture.Operations()).HandleAsync(
            RiskDraftSourceFixture.Context(creation, user), CancellationToken.None)).IsSuccess);
        var request = new ReviseRiskDraft(command, tenant, program, id, 1, "Revised title", "Scenario", "Effect");
        var handler = new ReviseRiskDraftHandler(fixture.Operations());
        Assert.True((await handler.HandleAsync(RiskDraftSourceFixture.Context(request, user), CancellationToken.None)).IsSuccess);
        Assert.True((await handler.HandleAsync(RiskDraftSourceFixture.Context(request with
        {
            CommandId = Uuid.CreateVersion4(),
            ExpectedRevision = 2,
            Title = "Later title"
        }, user), CancellationToken.None)).IsSuccess);

        // Act
        var original = await handler.HandleAsync(RiskDraftSourceFixture.Context(request, user, "Updated display"), CancellationToken.None);
        var changed = await handler.HandleAsync(RiskDraftSourceFixture.Context(request with
        {
            ExpectedRevision = change == "revision" ? 2 : request.ExpectedRevision,
            Title = change == "content" ? "Changed original intent" : request.Title,
        }, change == "actor" ? Uuid.CreateVersion4() : user), CancellationToken.None);
        var hydrated = await fixture.Reader.HydrateAsync(new RiskDraft(tenant, id, command));

        // Assert
        Assert.True(original.IsSuccess, original.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(3, hydrated.Revision);
        Assert.Empty(new AggregateScenario<RiskDraft>(hydrated).PendingEvents);
        Assert.Equal(2, Assert.IsType<RiskDraftRevised>(hydrated.Acceptance.SelectedFact).Revision);
        Assert.Equal(3, (await fixture.FactsAsync(tenant, id)).Count);
    }
}
