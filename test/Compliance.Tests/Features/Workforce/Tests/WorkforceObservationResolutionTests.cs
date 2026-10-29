using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
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
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
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
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class WorkforceObservationResolutionTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Fact]
    public void ShouldResolveWithAttributionAndReplayGivenOpenObservation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var resolution = new WorkforceObservationResolution(tenantId, Uuid.CreateVersion4());

        // Act
        var resolved = resolution.Resolve("resolved", " Access removed in IdP ", Author, Now);
        var replay = resolution.Resolve("resolved", "Access removed in IdP", Author, Now);
        var changed = resolution.Resolve("dismissed", "Not applicable", Author, Now);

        // Assert
        Assert.Null(resolved);
        Assert.Null(replay);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(changed).Code);
        var ev = Assert.IsType<WorkforceObservationResolved>(Assert.Single(
            new AggregateScenario<WorkforceObservationResolution>(resolution).PendingEvents));
        Assert.Equal("Access removed in IdP", ev.Note);
        Assert.Equal(Author, ev.Actor);
        Assert.Equal(resolution.Id, ev.ObservationId);
    }

    [Theory]
    [InlineData("reopened", "note")]
    [InlineData("resolved", " ")]
    public void ShouldRejectResolutionGivenInvalidOutcomeOrNote(string outcome, string note)
    {
        // Arrange
        var resolution = new WorkforceObservationResolution(Uuid.CreateVersion4(),
            Uuid.CreateVersion4());

        // Act
        var result = resolution.Resolve(outcome, note, Author, Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(result).Code);
    }

    [Fact]
    public async Task ShouldProjectResolutionsPerTenantGivenResolvedObservations()
    {
        // Arrange
        var directory = new FitzWorkforceObservationResolutionDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var observationId = Uuid.CreateVersion4();
        var open = Uuid.CreateVersion4();
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity("WorkforceObservationResolutionsV1",
                             EventStreamPattern.ForPattern(tenantId.ToString(),
                                 "workforce-observation-resolutions")),
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new WorkforceObservationResolved(tenantId, observationId,
                "dismissed", "Contractor tracked outside HR", Author, Now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var found = await directory.GetManyAsync(tenantId, [observationId, open]);
        var alien = await directory.GetManyAsync(Uuid.CreateVersion4(), [observationId]);

        // Assert
        var resolution = Assert.Single(found).Value;
        Assert.Equal("dismissed", resolution.Resolution);
        Assert.Equal(Author, resolution.ResolvedBy);
        Assert.Empty(alien);
    }

    [Fact]
    public void ShouldOverlayStatusGivenResolution()
    {
        // Arrange
        var resolution = new WorkforceObservationResolutionView(Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), "dismissed", "Accepted", Author, Now);

        // Act
        var open = WorkforceObservationStatus.Of(null);
        var dismissed = WorkforceObservationStatus.Of(resolution);

        // Assert
        Assert.Equal("open", open);
        Assert.Equal("dismissed", dismissed);
    }
}
