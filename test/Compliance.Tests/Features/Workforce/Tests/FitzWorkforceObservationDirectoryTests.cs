using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class FitzWorkforceObservationDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly DateOnly Start = new(2025, 1, 6);

    [Fact]
    public async Task ShouldObserveJoinerMoverAndLeaverGivenRosterRevisions()
    {
        // Arrange
        var directory = new FitzWorkforceObservationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var recorded = Recorded(tenantId, "E-100");
        var moved = recorded.Terms with { Department = "Finance" };
        var managerOnly = moved with { ManagerPersonId = Uuid.CreateVersion4() };
        var ended = managerOnly with { LifecycleStatus = "ended", EndDate = new DateOnly(2026, 9, 1) };
        var rehired = ended with { LifecycleStatus = "active", EndDate = null };

        // Act
        await ApplyAsync(directory, tenantId, recorded,
            Revised(recorded, 2, moved, 1), Revised(recorded, 3, managerOnly, 2),
            Revised(recorded, 4, ended, 3), Revised(recorded, 5, rehired, 4));
        var all = await directory.ListAsync(tenantId, null, 50, null);
        var leavers = await directory.ListAsync(tenantId, "leaver", 50, null);

        // Assert
        Assert.Equal(["joiner", "mover", "leaver", "joiner"], all.Items.Select(item => item.Kind));
        Assert.Equal([1L, 2L, 4L, 5L], all.Items.Select(item => item.RelationshipRevision));
        Assert.Equal(["department"], all.Items[1].ChangedFields);
        Assert.All(all.Items, item => Assert.Equal("open", item.Status));
        var leaver = Assert.Single(leavers.Items);
        Assert.Equal(new DateOnly(2026, 9, 1), leaver.EffectiveDate);
        Assert.Equal(recorded.PersonId, leaver.PersonId);
    }

    [Fact]
    public async Task ShouldReportOwnerStatusesAndKeepTenantsSeparateGivenSharedProjection()
    {
        // Arrange
        var directory = new FitzWorkforceObservationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var recorded = Recorded(tenantId, "E-100");
        var ended = recorded.Terms with { LifecycleStatus = "ended", EndDate = new DateOnly(2026, 9, 1) };
        await ApplyAsync(directory, tenantId, recorded, Revised(recorded, 2, ended, 1));

        // Act
        var statuses = await directory.ListRelationshipStatusesAsync(tenantId, recorded.PersonId);
        var alien = await directory.ListAsync(Uuid.CreateVersion4(), null, 50, null);
        var alienStatuses = await directory.ListRelationshipStatusesAsync(Uuid.CreateVersion4(),
            recorded.PersonId);

        // Assert
        Assert.Equal(["ended"], statuses);
        Assert.Empty(alien.Items);
        Assert.Empty(alienStatuses);
    }

    static WorkRelationshipRecorded Recorded(Uuid tenantId, string workerId) =>
        new(tenantId, WorkRelationship.IdFor(tenantId, workerId), Uuid.CreateVersion4(), workerId,
            new WorkRelationshipTerms("employee", "active", Start, null, "Engineering",
                Uuid.CreateVersion4(), null), Author, Now);

    static WorkRelationshipRevised Revised(WorkRelationshipRecorded recorded, long revision,
        WorkRelationshipTerms terms, int minutes) =>
        new(recorded.TenantId, recorded.RelationshipId, revision, terms, Author,
            Now.AddMinutes(minutes));

    static async ValueTask ApplyAsync(FitzWorkforceObservationDirectory directory, Uuid tenantId,
        params DomainEvent[] events)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("WorkforceObservationsV1",
                EventStreamPattern.ForPattern(tenantId.ToString(), "work-relationships")),
            ProjectionCheckpoint.Start));
        foreach (var domainEvent in events)
            await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
