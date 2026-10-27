using System.Security.Claims;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
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

public sealed class FitzServiceIdentityDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 9, 30);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Fact]
    public async Task ShouldListOnlyUnownedWorkGivenExpiredReviewAndEndedOwner()
    {
        // Arrange
        var kv = new InMemoryKvClient();
        var identities = new FitzServiceIdentityDirectory(kv);
        var roster = new FitzWorkforceObservationDirectory(kv);
        var tenantId = Uuid.CreateVersion4();
        var active = Uuid.CreateVersion4();
        var leaver = Uuid.CreateVersion4();
        await ApplyRosterAsync(roster, tenantId, Relationship(tenantId, active, "E-1", "active"),
            Relationship(tenantId, leaver, "E-2", "ended"));
        var owned = Recorded(tenantId, "a-owned", active, Today.AddMonths(3));
        var expired = Recorded(tenantId, "b-expired", active, Today.AddMonths(3));
        var orphaned = Recorded(tenantId, "c-orphaned", leaver, Today.AddMonths(3));
        await ApplyAsync(identities, tenantId, owned, expired, orphaned);
        var handler = Handler(identities, roster, new FixedClock(Now.AddMonths(4)));
        var laterHandler = Handler(identities, roster, new FixedClock(Now));

        // Act
        var afterReview = await handler.HandleAsync(Context(tenantId, true), CancellationToken.None);
        var beforeReview = await laterHandler.HandleAsync(Context(tenantId, null),
            CancellationToken.None);
        var alien = await laterHandler.HandleAsync(Context(Uuid.CreateVersion4(), null),
            CancellationToken.None);

        // Assert
        Assert.Equal(["a-owned", "b-expired", "c-orphaned"],
            afterReview.Value.Items.Select(item => item.DisplayName));
        Assert.Equal(["owner_relationship_ended", "review_expired"],
            afterReview.Value.Items[2].UnownedReasons);
        Assert.Equal([false, false, true], beforeReview.Value.Items.Select(item => item.Unowned));
        Assert.Empty(alien.Value.Items);
    }

    [Fact]
    public async Task ShouldProjectExpiryAndReportExpiredGivenExpiryDatePassed()
    {
        // Arrange
        var kv = new InMemoryKvClient();
        var identities = new FitzServiceIdentityDirectory(kv);
        var roster = new FitzWorkforceObservationDirectory(kv);
        var tenantId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        await ApplyRosterAsync(roster, tenantId, Relationship(tenantId, ownerId, "E-1", "active"));
        var expiring = Recorded(tenantId, "a-expiring", ownerId, Today.AddMonths(6));
        expiring = expiring with { Terms = expiring.Terms with { ExpiresOn = Today.AddMonths(1) } };
        var lasting = Recorded(tenantId, "b-lasting", ownerId, Today.AddMonths(6));
        await ApplyAsync(identities, tenantId, expiring, lasting);
        var handler = Handler(identities, roster, new FixedClock(Now.AddMonths(2)));

        // Act
        var page = await handler.HandleAsync(Context(tenantId, null), CancellationToken.None);

        // Assert
        Assert.Equal(Today.AddMonths(1), page.Value.Items[0].ExpiresOn);
        Assert.Equal([true, false], page.Value.Items.Select(item => item.Expired));
        Assert.Null(page.Value.Items[1].ExpiresOn);
    }

    static ListServiceIdentitiesHandler Handler(FitzServiceIdentityDirectory identities,
        FitzWorkforceObservationDirectory roster, TimeProvider clock)
    {
        var events = new InMemoryEventStore();
        return new ListServiceIdentitiesHandler(identities,
            new ServiceIdentityReadConsistency(identities, new NoSource(), events),
            new ServiceIdentityOwnership(roster, new NoSource(),
                new WorkforceObservationReadConsistency(roster, events), clock));
    }

    static RequestContext<ListServiceIdentities> Context(Uuid tenantId, bool? unownedOnly) =>
        new(new ListServiceIdentities(tenantId, unownedOnly), Actor());

    static ServiceIdentityRecorded Recorded(Uuid tenantId, string name, Uuid ownerId,
        DateOnly reviewBy) =>
        new(tenantId, Uuid.CreateVersion4(), new ServiceIdentityTerms(name, "workload",
            "Nightly export", null, "active", "person", ownerId, reviewBy), Author, Now);

    static WorkRelationshipRecorded Relationship(Uuid tenantId, Uuid personId, string workerId,
        string status) =>
        new(tenantId, WorkRelationship.IdFor(tenantId, workerId), personId, workerId,
            new WorkRelationshipTerms("employee", status, new DateOnly(2025, 1, 6),
                status == "ended" ? new DateOnly(2026, 9, 1) : null, null, null, null), Author, Now);

    static async ValueTask ApplyAsync(FitzServiceIdentityDirectory directory, Uuid tenantId,
        params DomainEvent[] events)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("ServiceIdentityDirectoryV1",
                EventStreamPattern.ForPattern(tenantId.ToString(), "service-identities")),
            ProjectionCheckpoint.Start));
        foreach (var domainEvent in events)
            await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static async ValueTask ApplyRosterAsync(FitzWorkforceObservationDirectory directory,
        Uuid tenantId, params DomainEvent[] events)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("WorkforceObservationsV1",
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

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));
}
