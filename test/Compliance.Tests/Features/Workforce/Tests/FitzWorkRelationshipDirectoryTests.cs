using System.Security.Claims;
using Cntryl.Fitz.Extensions;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
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
using Bdgrz.Compliance.Features.Workforce.Services;
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
using Bdgrz.Compliance.Features.Workforce.Handlers;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Queries;
namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class FitzWorkRelationshipDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Fact]
    public async Task ShouldOrderByWorkerIdAndKeepTenantsSeparateGivenSharedProjection()
    {
        // Arrange
        var directory = new FitzWorkRelationshipDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        await ApplyAsync(directory, tenantId, Recorded(tenantId, "E-200"), Recorded(tenantId, "E-100"));

        // Act
        var page = await directory.ListAsync(tenantId, 50, null);
        var alien = await directory.ListAsync(Uuid.CreateVersion4(), 50, null);

        // Assert
        Assert.Equal(["E-100", "E-200"], page.Items.Select(item => item.SourceWorkerId));
        Assert.All(page.Items, item => Assert.Equal("manual", item.SourceKind));
        Assert.Empty(alien.Items);
    }

    [Fact]
    public async Task ShouldApplyLeaverRevisionAndRejectOutOfOrderGivenRecordedRelationship()
    {
        // Arrange
        var directory = new FitzWorkRelationshipDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var recorded = Recorded(tenantId, "E-100");
        var ended = recorded.Terms with { LifecycleStatus = "ended", EndDate = new DateOnly(2026, 9, 1) };
        await ApplyAsync(directory, tenantId, recorded,
            new WorkRelationshipRevised(tenantId, recorded.RelationshipId, 2, ended, Author, Now));

        // Act
        var view = await directory.GetAsync(tenantId, recorded.RelationshipId);
        var outOfOrder = await Record.ExceptionAsync(() => ApplyAsync(directory, tenantId,
            new WorkRelationshipRevised(tenantId, recorded.RelationshipId, 4, ended, Author, Now)).AsTask());

        // Assert
        Assert.Equal(2, view!.Revision);
        Assert.Equal("ended", view.LifecycleStatus);
        Assert.Equal(new DateOnly(2026, 9, 1), view.EndDate);
        Assert.IsType<InvalidOperationException>(outOfOrder);
    }

    [Fact]
    public async Task ShouldRedactManagerGivenListButNotGivenSingleRead()
    {
        // Arrange
        var directory = new FitzWorkRelationshipDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var recorded = Recorded(tenantId, "E-100");
        await ApplyAsync(directory, tenantId, recorded);
        var handler = new ListWorkRelationshipsHandler(directory,
            new WorkRelationshipReadConsistency(directory, new NoSource(), new InMemoryEventStore()));
        var context = new RequestContext<ListWorkRelationships>(new ListWorkRelationships(tenantId), Actor());

        // Act
        var listed = await handler.HandleAsync(context, CancellationToken.None);
        var single = await directory.GetAsync(tenantId, recorded.RelationshipId);

        // Assert
        var item = Assert.Single(listed.Value.Items);
        Assert.Null(item.ManagerPersonId);
        Assert.True(item.RestrictedFieldsRedacted);
        Assert.Equal(recorded.Terms.ManagerPersonId, single!.ManagerPersonId);
        Assert.False(single.RestrictedFieldsRedacted);
    }

    static WorkRelationshipRecorded Recorded(Uuid tenantId, string workerId) =>
        new(tenantId, WorkRelationship.IdFor(tenantId, workerId), Uuid.CreateVersion4(), workerId,
            new WorkRelationshipTerms("employee", "active", new DateOnly(2025, 1, 6), null,
                "Engineering", Uuid.CreateVersion4(), null), Author, Now);

    static async ValueTask ApplyAsync(FitzWorkRelationshipDirectory directory, Uuid tenantId,
        params DomainEvent[] events)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("WorkRelationshipDirectoryV1",
                EventStreamPattern.ForPattern(tenantId.ToString(), "work-relationships")),
            ProjectionCheckpoint.Start));
        foreach (var domainEvent in events)
            await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    sealed class NoSource : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate => ValueTask.FromResult(aggregate);
    }

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));
}
