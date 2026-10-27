using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
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
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

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
