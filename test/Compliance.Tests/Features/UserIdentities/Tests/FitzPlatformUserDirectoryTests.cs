using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
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
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;

public sealed class FitzPlatformUserDirectoryTests
{
    [Fact]
    public async Task ShouldUpdateAndClearCurrentNameGivenRetainedProfileObservations()
    {
        // Arrange
        var directory = new FitzPlatformUserDirectory(new InMemoryKvClient());
        var userId = Uuid.CreateVersion4();
        var otherUser = Uuid.CreateVersion4();
        var identity = new CheckpointIdentity("PlatformUserDirectoryV2",
            EventStreamPattern.ForPattern("bdgrz", "user-identities"));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new UserIdentityRegistered(userId, "issuer", "subject", null));
            await directory.ApplyAsync(Observation(userId, "Ada"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Equal("Ada", await directory.ReadAsync(userId));
        Assert.Null(await directory.ReadAsync(otherUser));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(Observation(userId, "Ada King"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        Assert.Equal("Ada King", await directory.ReadAsync(userId));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(Observation(userId, null));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        Assert.Null(await directory.ReadAsync(userId));
        Assert.True(await directory.ExistsAsync(userId));
    }

    [Fact]
    public async Task ShouldFindOnePlatformUserGivenTwoLinkedProviderIdentities()
    {
        // Arrange
        var directory = new FitzPlatformUserDirectory(new InMemoryKvClient());
        var userId = Uuid.CreateVersion4();
        var identity = new CheckpointIdentity("PlatformUserDirectoryV2",
            EventStreamPattern.ForPattern("bdgrz", "user-identities"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new UserIdentityRegistered(userId,
                "issuer-a", "subject-a", null));
            await directory.ApplyAsync(new UserIdentityRegistered(userId,
                "issuer-b", "subject-b", null));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.True(await directory.ExistsAsync(userId));
        Assert.False(await directory.ExistsAsync(Uuid.CreateVersion4()));
    }

    static UserIdentityProfileObserved Observation(Uuid userId, string? displayName)
    {
        var identity = new UserIdentity("issuer", "subject");
        Assert.True(identity.Register(userId, null).IsSuccess);
        var scenario = new Cntryl.Portia.Testing.AggregateScenario<UserIdentity>(identity);
        // Profile admission follows the committed registration, as in production.
        var registered = Assert.Single(scenario.PendingEvents);
        var source = new UserIdentity("issuer", "subject");
        var admitted = new Cntryl.Portia.Testing.AggregateScenario<UserIdentity>(source).Given(registered);
        Assert.True(source.ObserveAuthenticatedProfile(displayName, DateTimeOffset.UtcNow).IsSuccess);
        return Assert.IsType<UserIdentityProfileObserved>(Assert.Single(admitted.PendingAudits));
    }

}
