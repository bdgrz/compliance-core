using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
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
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class FitzTeamMemberDirectoryReaderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReturnEveryMemberGivenTeamQuery()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        await SeedAsync(client, TeamId, first);
        await SeedAsync(client, TeamId, second);
        var reader = new FitzTeamMemberDirectoryReader(client, new InMemoryEventStore());

        // Act
        var page = await reader.ListAsync(
            TenantId, TeamId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        Assert.Equal(2, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ShouldFindMemberRemovalAfterCheckpointGivenLaggingTeamProjection()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var events = new InMemoryEventStore();
        var memberId = Uuid.CreateVersion4();
        var reader = new FitzTeamMemberDirectoryReader(client, events);
        var pattern = EventStreamPattern.ForPattern(TenantId.ToString());
        var identity = new CheckpointIdentity("TeamMemberDirectory", pattern);
        await using (var batch = await reader.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await reader.ApplyAsync(TeamMemberFacts.Assigned(TenantId, TeamId, memberId));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var assignmentId = RbacIds.TeamMember(TenantId, TeamId, memberId);
        DomainEvent removed = TeamMemberFacts.Removed(TenantId, TeamId, memberId, Uuid.Empty, Uuid.CreateVersion4());
        removed.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), assignmentId, 1,
            DateTimeOffset.UtcNow));
        await events.AppendAsync(new EventStreamAddress(TenantId.ToString(), "rbac-team-members",
            assignmentId.ToString()), 0, [removed]);

        // Act
        var pendingRemoval = await reader.HasPendingRemovalAsync(TenantId, TeamId, memberId);

        // Assert
        Assert.True(pendingRemoval);
    }

    [Fact]
    public async Task ShouldReturnCurrentAssignmentsGivenProjectionAndEventTail()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var events = new InMemoryEventStore();
        var memberId = Uuid.CreateVersion4();
        var nextTeamId = Uuid.CreateVersion4();
        var reader = new FitzTeamMemberDirectoryReader(client, events);
        var pattern = EventStreamPattern.ForPattern(TenantId.ToString());
        var identity = new CheckpointIdentity("TeamMemberDirectory", pattern);
        await using (var batch = await reader.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await reader.ApplyAsync(TeamMemberFacts.Assigned(TenantId, TeamId, memberId));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var removed = TeamMemberFacts.Removed(TenantId, TeamId, memberId, Uuid.Empty, Uuid.CreateVersion4());
        removed.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(),
            RbacIds.TeamMember(TenantId, TeamId, memberId), 1, DateTimeOffset.UtcNow));
        var assigned = TeamMemberFacts.Assigned(TenantId, nextTeamId, memberId);
        assigned.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(),
            RbacIds.TeamMember(TenantId, nextTeamId, memberId), 1, DateTimeOffset.UtcNow));
        await events.AppendAsync(new EventStreamAddress(TenantId.ToString(), "rbac-team-members",
            RbacIds.TeamMember(TenantId, TeamId, memberId).ToString()), 0, [removed]);
        await events.AppendAsync(new EventStreamAddress(TenantId.ToString(), "rbac-team-members",
            RbacIds.TeamMember(TenantId, nextTeamId, memberId).ToString()), 0, [assigned]);

        // Act
        var assignments = await reader.ListMemberAssignmentsAsync(TenantId, memberId);

        // Assert
        var assignment = Assert.Single(assignments);
        Assert.Equal(nextTeamId, assignment.TeamId);
    }

    [Fact]
    public async Task ShouldHonorLaterAssignmentGivenRemovalAndReassignmentBeforeProjectionCatchesUp()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var events = new InMemoryEventStore();
        var memberId = Uuid.CreateVersion4();
        var reader = new FitzTeamMemberDirectoryReader(client, events);
        var pattern = EventStreamPattern.ForPattern(TenantId.ToString());
        var identity = new CheckpointIdentity("TeamMemberDirectory", pattern);
        await using (var batch = await reader.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await reader.ApplyAsync(TeamMemberFacts.Assigned(TenantId, TeamId, memberId));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var assignmentId = RbacIds.TeamMember(TenantId, TeamId, memberId);
        var address = new EventStreamAddress(TenantId.ToString(), "rbac-team-members",
            assignmentId.ToString());
        DomainEvent removed = TeamMemberFacts.Removed(TenantId, TeamId, memberId, Uuid.Empty, Uuid.CreateVersion4());
        removed.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), assignmentId, 1,
            DateTimeOffset.UtcNow));
        DomainEvent reassigned = TeamMemberFacts.Assigned(TenantId, TeamId, memberId);
        reassigned.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), assignmentId, 2,
            DateTimeOffset.UtcNow.AddSeconds(1)));
        await events.AppendAsync(address, 0, [removed, reassigned]);

        // Act
        var pendingRemoval = await reader.HasPendingRemovalAsync(TenantId, TeamId, memberId);

        // Assert
        Assert.False(pendingRemoval);
    }

    [Fact]
    public async Task ShouldReturnMembersOnlyGivenRequestedTeam()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var otherTeamId = Uuid.CreateVersion4();
        var member = Uuid.CreateVersion4();
        var otherMember = Uuid.CreateVersion4();
        await SeedAsync(client, TeamId, member);
        await SeedAsync(client, otherTeamId, otherMember);
        var reader = new FitzTeamMemberDirectoryReader(client, new InMemoryEventStore());

        // Act
        var page = await reader.ListAsync(
            TenantId, TeamId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        var result = Assert.Single(page.Items);
        Assert.Equal(member, result.MemberId);
    }

    [Fact]
    public async Task ShouldAvoidEmptyPageGivenReturnedCursor()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await SeedAsync(client, TeamId, Uuid.CreateVersion4());
        await SeedAsync(client, TeamId, Uuid.CreateVersion4());
        var reader = new FitzTeamMemberDirectoryReader(client, new InMemoryEventStore());

        // Act
        var firstPage = await reader.ListAsync(
            TenantId, TeamId, 1, null, null, descending: false, CancellationToken.None);

        // Assert
        Assert.Single(firstPage.Items);
        Assert.NotNull(firstPage.NextCursor);

        var secondPage = await reader.ListAsync(
            TenantId, TeamId, 1, firstPage.NextCursor, null, descending: false, CancellationToken.None);

        Assert.Single(secondPage.Items);
        Assert.Null(secondPage.NextCursor);
    }

    // Regression test: Search must match a substring anywhere in the member ID, not just a prefix —
    // the KvDirectory 1.3.0 migration briefly narrowed this to prefix-only before being caught and
    // fixed.
    [Fact]
    public async Task ShouldFilterMembersGivenIdSubstring()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var member = Uuid.CreateVersion4();
        var otherMember = Uuid.CreateVersion4();
        await SeedAsync(client, TeamId, member);
        await SeedAsync(client, TeamId, otherMember);
        var reader = new FitzTeamMemberDirectoryReader(client, new InMemoryEventStore());
        var middleOfMemberId = member.ToString().Substring(9, 8);

        // Act
        var page = await reader.ListAsync(
            TenantId, TeamId, null, null, middleOfMemberId, descending: false, CancellationToken.None);

        // Assert
        var result = Assert.Single(page.Items);
        Assert.Equal(member, result.MemberId);
    }

    static async Task SeedAsync(InMemoryKvClient client, Uuid teamId, Uuid memberId)
    {
        var repository = new FitzTeamMemberDirectoryReader(client, new InMemoryEventStore());
        var identity = new CheckpointIdentity("TeamMemberDirectory", EventStreamPattern.ForPattern(TenantId.ToString()));
        await using var batch = await repository.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await repository.ApplyAsync(TeamMemberFacts.Assigned(TenantId, teamId, memberId));
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
