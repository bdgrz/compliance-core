using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Handlers;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskDraftCreationIdentityTests
{
    [Fact]
    public async Task ShouldRetainOriginalRegistrationGivenTypedCreationRetryAfterRevision()
    {
        // Arrange
        await using var fixture = new RiskDraftSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var id = RiskDraft.IdFor(tenant, program, "R-01");
        await fixture.SeedProgramAsync(tenant, program, RbacIds.Member(tenant, user));
        var request = new CreateRiskDraft(id, tenant, program, "r-01", "Original title", "Scenario", "Effect");
        var create = new CreateRiskDraftHandler(fixture.Operations());
        Assert.True((await create.HandleAsync(RiskDraftSourceFixture.Context(request, user), CancellationToken.None)).IsSuccess);
        var revise = new ReviseRiskDraftHandler(fixture.Operations());
        Assert.True((await revise.HandleAsync(RiskDraftSourceFixture.Context(new ReviseRiskDraft(Uuid.CreateVersion4(), tenant, program, id,
            1, "Later title", "Later scenario", "Effect"), user), CancellationToken.None)).IsSuccess);

        // Act
        var original = await create.HandleAsync(RiskDraftSourceFixture.Context(request, user, "Updated profile"), CancellationToken.None);
        var changedActor = await create.HandleAsync(RiskDraftSourceFixture.Context(request, Uuid.CreateVersion4()), CancellationToken.None);
        var changedIntent = await create.HandleAsync(RiskDraftSourceFixture.Context(request with { Title = "Different creation" }, user), CancellationToken.None);
        var reloaded = await fixture.Reader.HydrateAsync(new RiskDraft(tenant, id, id));

        // Assert
        Assert.True(original.IsSuccess, original.Error?.Message);
        Assert.Equal(new RiskRegistration(id, "R-01", 1), original.Value);
        Assert.Equal(RequestErrorKind.Conflict, changedActor.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedIntent.Error?.Kind);
        Assert.Equal(2, reloaded.Revision);
        Assert.Empty(new AggregateScenario<RiskDraft>(reloaded).PendingEvents);
        Assert.Equal(2, (await fixture.FactsAsync(tenant, id)).Count);
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendGivenAuthoredIdentityDiffersFromNormalizedIdentifierIdentity()
    {
        // Arrange
        await using var fixture = new RiskDraftSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var wrong = Uuid.CreateVersion4();
        var request = new CreateRiskDraft(wrong, tenant, program, "R-01", "Title", "Scenario", "Effect");
        var handler = new CreateRiskDraftHandler(fixture.Operations());

        // Act
        var result = await handler.HandleAsync(RiskDraftSourceFixture.Context(request, Uuid.CreateVersion4()), CancellationToken.None);
        var risk = await fixture.Reader.HydrateAsync(new RiskDraft(tenant, wrong));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0, risk.Revision);
        Assert.Empty(new AggregateScenario<RiskDraft>(risk).PendingEvents);
    }
}
