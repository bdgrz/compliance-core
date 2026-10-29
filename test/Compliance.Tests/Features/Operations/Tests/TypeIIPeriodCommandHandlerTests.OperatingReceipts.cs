using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Artifacts.Persistence;
using Bdgrz.Compliance.Features.Artifacts.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Changes.Services;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Responsibilities.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Changes.Readers;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Incidents.Readers;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

public sealed partial class TypeIIPeriodCommandHandlerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldHonorAuthenticatedOperatingReceiptGivenContradictoryAcceptedTimestamp(bool lateCommit)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedSelectedPeriodAsync();
        var boundary = Uuid.CreateVersion4();
        await SeedCloseEngagementAsync(fixture, boundary);
        var period = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);
        var request = await CloseRequestAsync(fixture);
        var operation = RootOperation.Provenance(request.CommandId, nameof(RecordTypeIIPeriodCloseValidation), period.Stream,
            ActorReference.ForMember(fixture.ActorMemberId, "Close validator"), period.Revision, new string('a', 64),
            fixture.CloseClock.GetUtcNow());
        var broker = Assert.IsType<ReceiptTrackingEventStore>(fixture.Host.Services.GetRequiredService<IEventStore>());
        broker.Clock = new FixedTimeProvider(operation.AcceptedAt.AddMilliseconds(lateCommit ? 1 : -1));
        var declaredAt = lateCommit ? StartedAt : operation.AcceptedAt.AddMilliseconds(1);
        var previous = await ControlOperatingSeed.CurrentPlanAsync(fixture.Host.Provider, fixture.TenantId,
            fixture.ProgramId, fixture.ControlId);
        var pending = await ControlOperatingSeed.ProposeAsync(fixture.Host.Provider, fixture.TenantId, fixture.ProgramId,
            fixture.ControlId, fixture.ControlVersionId, previous.ExpectedEvidence, Uuid.CreateVersion4(), previous.Owner,
            previous.BackupOwner, previous.ReviewerMemberId, previous.Cadence, previous.EffectiveFrom.AddDays(1),
            "Declared and committed source clocks differ.", fixture.ProposerMemberId, "Source preparer", declaredAt);
        var directory = Path.Combine(Path.GetTempPath(), "bdgrz-operating-close-receipt-" + Guid.NewGuid().ToString("N"));
        try
        {
            var artifacts = new LocalArtifactContentStore(new ArtifactContentStoreOptions(directory, new byte[32],
                ArtifactContentLimits.MaxContentLength), fixture.CloseClock);
            var families = new PublishedTypeIBaselinePreviewReader(new TypeIBaselinePreviewReader(
                TypeIBaselineSourceFamilies.Required.Select(family => new CloseFamily(family, boundary)).ToArray()), artifacts,
                fixture.Host.Content);
            var history = new TypeIIPeriodCloseHistory(fixture.Events,
                new RestrictedComplianceRecordVisibility(new RecordingPermissionAuthorizer(true)),
                new SignificantChangeReader(fixture.Reader, fixture.Events, fixture.Host.Content,
                    fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>()), fixture.Host.Content,
                new Bdgrz.Compliance.Features.Incidents.Readers.ComplianceIncidentReader(fixture.Reader, fixture.Events, fixture.Host.Content,
                    fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>()),
                fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>());
            var source = new TypeIIPeriodCloseReconciliation(fixture.Reader, fixture.Events, fixture.Host.Content,
                fixture.Host.Services.GetRequiredService<ControlOperationsReader>(), fixture.Host.Services.GetRequiredService<FindingReader>(),
                families, history, new TypeIIPeriodCloseSourcePublication(artifacts, fixture.Host.Content),
                new RecordedSourceReceiptCutFactory(fixture.Events, broker));

            // Act
            var result = await source.ReadAsync(period, fixture.Context(request).Actor, fixture.ActorUserId,
                operation, CancellationToken.None);
            var retained = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);

            // Assert
            Assert.Equal(!lateCommit, result.IsSuccess);
            if (lateCommit)
            {
                Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
                Assert.True(result.Error!.IsTransient);
            }
            else
            {
                Assert.Equal(operation.AcceptedAt, result.Value.RecordedThrough);
                Assert.NotNull(result.Value.SourceContentManifest);
            }
            Assert.Equal(declaredAt, pending.ProposedAt);
            Assert.Null(retained.View!.CloseValidation);
            Assert.Equal(period.Owner.CommittedStreamPosition, retained.Owner.CommittedStreamPosition);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
