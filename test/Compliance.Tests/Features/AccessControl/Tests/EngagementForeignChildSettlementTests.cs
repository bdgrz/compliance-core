using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class EngagementForeignChildSettlementTests
{
    [Fact]
    public async Task ShouldRejectForeignMaterializedChildGivenSamePersonalIdentityOnDifferentFirstOwners()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var lead = await host.RegisterStaffAsync();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        foreach (var id in new[] { first, second })
            Assert.True((await host.SendAsync(new CreateServiceEngagement(host.Tenant, id, 0, IndependenceHost.Draft(lead)))).IsSuccess);
        var command = await PersonalDecisionFacts.AcknowledgementAsync(host, first);
        Assert.True((await host.HttpAsync(command)).IsSuccess);
        var completed = await PersonalDecisionFacts.RunAsync(host.Provider, host.Tenant, first, command.AcknowledgementId);
        Assert.Equal(Bdgrz.Compliance.Features.Processes.Processes.ProcessState.Completed, completed.State);
        var child = await host.HydrateAsync(new EngagementManagementAcknowledgement(host.Tenant, command.AcknowledgementId));
        var secondCommand = command with { EngagementId = second };
        Assert.True((await host.HttpAsync(secondCommand)).IsSuccess);
        _ = await PersonalDecisionFacts.RunAsync(host.Provider, host.Tenant, second, command.AcknowledgementId);
        var pending = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, second));
        var decision = pending.DecisionOf(command.AcknowledgementId)!;
        Assert.NotNull(decision.Authorized);
        Assert.True(decision.IsPending);
        var before = (await host.EventsAsync<DomainEvent>(pending.Stream)).Count;
        var foreign = new SettleEngagementPersonalDecision(Uuid.CreateVersion4(), NativeFactReference.Of(child.Stream, child.Acknowledged!),
            pending.Stream, command.AcknowledgementId, 1, null, decision.Admitted.Intent, second);

        // Act
        var refused = await PersonalDecisionFacts.SendReceiverAsync(host.Provider, foreign);

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.False(refused.Error!.IsTransient);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(pending.Stream)).Count);
    }
}
