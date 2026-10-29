using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Handlers;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

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
