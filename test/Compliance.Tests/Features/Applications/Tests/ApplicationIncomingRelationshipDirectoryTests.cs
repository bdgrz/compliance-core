using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using System.Globalization;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
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
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
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
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Projectors;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

public sealed class ApplicationIncomingRelationshipDirectoryTests
{
    [Fact]
    public async Task ShouldProjectIncomingRelationshipLifecycleAndOrderActiveRowsGivenRelationshipEvents()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var firstSourceId = Uuid.CreateVersion4();
        var secondSourceId = Uuid.CreateVersion4();
        var removedSourceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var approverId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var directory = new FitzApplicationIncomingRelationshipDirectory(new InMemoryKvClient());
        var dependsOn = Recorded(tenantId, firstSourceId, targetId, "depends_on", "active",
            actorId, "Author", now);
        var proposedReplaces = Recorded(tenantId, secondSourceId, targetId, "replaces",
            "proposed", actorId, "Author", now.AddMinutes(1));
        var approvedReplaces = new ApplicationRelationshipApproved(tenantId,
            proposedReplaces.RelationshipId, secondSourceId, targetId, 2, 2, 3,
            new string('a', 64), approverId, "Approver", now.AddMinutes(2));
        var removed = new ApplicationRelationshipGovernedRemoved(tenantId,
            RelationshipId(removedSourceId, "depends_on", targetId), removedSourceId,
            targetId, "depends_on", 2, "No longer required", actorId, "Remover",
            now.AddMinutes(3));
        var removedRecord = Recorded(tenantId, removedSourceId, targetId, "depends_on",
            "active", actorId, "Author", now.AddMinutes(2));
        var sameSourceReplaces = Recorded(tenantId, firstSourceId, targetId, "replaces",
            "proposed", actorId, "Author", now.AddMinutes(4));

        // Act
        await ApplyAsync(directory, tenantId, dependsOn, dependsOn, sameSourceReplaces,
            proposedReplaces, approvedReplaces, approvedReplaces, removedRecord, removed,
            removed);
        var incoming = await directory.ListActiveIncomingAsync(tenantId, targetId, 50, null);

        // Assert
        Assert.Equal(3, incoming.Items.Count);
        Assert.DoesNotContain(incoming.Items,
            relationship => relationship.SourceApplicationId == removedSourceId);
        Assert.Equal(incoming.Items.OrderBy(static relationship =>
                relationship.SourceApplicationId.ToString(), StringComparer.Ordinal)
            .ThenBy(static relationship => relationship.RelationshipId.ToString(),
                StringComparer.Ordinal).Select(static relationship => relationship.RelationshipId),
            incoming.Items.Select(static relationship => relationship.RelationshipId));
        Assert.Equal("active", Assert.Single(incoming.Items, relationship =>
            relationship.RelationshipId == dependsOn.RelationshipId).Status);
        Assert.Equal("approved", Assert.Single(incoming.Items, relationship =>
            relationship.RelationshipId == proposedReplaces.RelationshipId).Status);
        Assert.Equal(approverId, Assert.Single(incoming.Items, relationship =>
            relationship.RelationshipId == proposedReplaces.RelationshipId).ActorMemberId);
        Assert.Equal("proposed", Assert.Single(incoming.Items, relationship =>
            relationship.RelationshipId == sameSourceReplaces.RelationshipId).Status);
    }

    [Fact]
    public async Task ShouldIsolateIncomingRelationshipsByTenantGivenSameRelationshipId()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var directory = new FitzApplicationIncomingRelationshipDirectory(new InMemoryKvClient());
        var first = Recorded(tenantId, sourceId, targetId, "depends_on", "active",
            Uuid.CreateVersion4(), "First tenant", DateTimeOffset.UtcNow);
        var second = first with
        {
            TenantId = otherTenantId,
            ActorMemberId = Uuid.CreateVersion4(),
            ActorDisplay = "Other tenant",
        };

        // Act
        await ApplyAsync(directory, tenantId, first);
        var beforeOtherTenantProjection = await directory.ListActiveIncomingAsync(
            otherTenantId, targetId, 20, null);
        await ApplyAsync(directory, otherTenantId, second);
        var firstTenantRows = await directory.ListActiveIncomingAsync(tenantId, targetId,
            20, null);
        var otherTenantRows = await directory.ListActiveIncomingAsync(otherTenantId,
            targetId, 20, null);

        // Assert
        Assert.Empty(beforeOtherTenantProjection.Items);
        Assert.Equal(first.ActorMemberId,
            Assert.Single(firstTenantRows.Items).ActorMemberId);
        Assert.Equal(second.ActorMemberId,
            Assert.Single(otherTenantRows.Items).ActorMemberId);
    }

    [Fact]
    public async Task ShouldReturnOnlyFirstTwoHundredRowsGivenLargerRequestedLimit()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var namespaceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var directory = new FitzApplicationIncomingRelationshipDirectory(new InMemoryKvClient());
        var sourceIds = Enumerable.Range(0, 205)
            .Select(index => Uuid.CreateVersion5(namespaceId,
                index.ToString(CultureInfo.InvariantCulture)))
            .OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray();
        var events = sourceIds.Select((sourceId, index) => (DomainEvent)Recorded(tenantId,
            sourceId, targetId, "depends_on", "active", actorId, "Author",
            now.AddTicks(index))).ToArray();

        // Act
        await ApplyAsync(directory, tenantId, events);
        var incoming = await directory.ListActiveIncomingAsync(tenantId, targetId, 10_000, null);

        // Assert
        Assert.Equal(200, incoming.Items.Count);
        Assert.Equal(sourceIds.Take(200), incoming.Items.Select(static relationship =>
            relationship.SourceApplicationId));
    }

    [Fact]
    public async Task ShouldRejectMismatchedRelationshipTargetGivenRecordedEvent()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var valid = Recorded(tenantId, sourceId, targetId, "depends_on", "active",
            Uuid.CreateVersion4(), "Author", now);
        var invalid = valid with { TargetApplicationId = Uuid.CreateVersion4() };
        var directory = new FitzApplicationIncomingRelationshipDirectory(new InMemoryKvClient());

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            Identity(tenantId), ProjectionCheckpoint.Start));

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyAsync(invalid));
    }

    static async Task ApplyAsync(FitzApplicationIncomingRelationshipDirectory directory,
        Uuid tenantId, params DomainEvent[] domainEvents)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            Identity(tenantId), ProjectionCheckpoint.Start));
        foreach (var domainEvent in domainEvents)
            await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static CheckpointIdentity Identity(Uuid tenantId) => new(
        ApplicationIncomingRelationshipProjector.ProjectorName,
        EventStreamPattern.ForPattern(tenantId.ToString(), "applications"));

    static ApplicationRelationshipRecorded Recorded(Uuid tenantId, Uuid sourceId,
        Uuid targetId, string relationshipType, string status, Uuid actorId,
        string actorDisplay, DateTimeOffset changedAt) => new(tenantId,
        RelationshipId(sourceId, relationshipType, targetId), sourceId, targetId,
        relationshipType, 1, status,
        relationshipType == "replaces" ? 2 : 1,
        relationshipType == "replaces" ? 3 : 1,
        actorId, actorDisplay, changedAt);

    static Uuid RelationshipId(Uuid sourceId, string relationshipType,
        Uuid targetId) => Uuid.CreateVersion5(sourceId,
        $"application-relationship:{relationshipType}:{targetId}");
}
