using System.Security.Claims;
using System.Text;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Artifacts.Persistence;
using Bdgrz.Compliance.Features.Artifacts.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Changes.Rules;
using Bdgrz.Compliance.Features.Changes.Services;
using Bdgrz.Compliance.Features.Examinations.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
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
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.Changes.Readers;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Incidents.Readers;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

public sealed partial class TypeIIPeriodCommandHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldDeriveMissingWorkFromNativeSelectionsGivenUnmaterializedOrUnreviewedOccurrences(bool materialized)
    {
        // Arrange
        await using var fixture = new Fixture();
        if (materialized)
            await fixture.SeedCoveredCloseReadyPeriodAsync();
        else
            await fixture.SeedSelectedPeriodAsync();
        var boundaryId = Uuid.CreateVersion4();
        await SeedCloseEngagementAsync(fixture, boundaryId);
        var directory = Path.Combine(Path.GetTempPath(), "bdgrz-close-reconciliation-" + Guid.NewGuid().ToString("N"));
        try
        {
            var artifacts = new LocalArtifactContentStore(new ArtifactContentStoreOptions(directory, new byte[32], ArtifactContentLimits.MaxContentLength), fixture.CloseClock);
            var contributors = TypeIBaselineSourceFamilies.Required.Select(family => new CloseFamily(family, boundaryId)).ToArray();
            var published = new PublishedTypeIBaselinePreviewReader(new TypeIBaselinePreviewReader(contributors), artifacts, fixture.Host.Content);
            var source = new TypeIIPeriodCloseReconciliation(fixture.Reader, fixture.Events, fixture.Host.Content,
                fixture.Host.Services.GetRequiredService<ControlOperationsReader>(), fixture.Host.Services.GetRequiredService<FindingReader>(), published,
                new TypeIIPeriodCloseHistory(fixture.Events, new RestrictedComplianceRecordVisibility(new RecordingPermissionAuthorizer(true)),
                    new SignificantChangeReader(fixture.Reader, fixture.Events, fixture.Host.Content, fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>()), fixture.Host.Content, new ComplianceIncidentReader(fixture.Reader, fixture.Events, fixture.Host.Content, fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>()), fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>()),
                new TypeIIPeriodCloseSourcePublication(artifacts, fixture.Host.Content),
                new RecordedSourceReceiptCutFactory(fixture.Events, fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>()));
            var period = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);
            var request = await CloseRequestAsync(fixture);
            var operation = RootOperation.Provenance(request.CommandId, nameof(RecordTypeIIPeriodCloseValidation), period.Stream,
                ActorReference.ForMember(fixture.ActorMemberId, "Close validator"), period.Revision, new string('a', 64), fixture.CloseClock.GetUtcNow());

            // Act
            var reconciliation = await source.ReadAsync(period, fixture.Context(request).Actor, fixture.ActorUserId, operation, CancellationToken.None);

            // Assert
            Assert.True(reconciliation.IsSuccess, reconciliation.Error?.Message);
            Assert.Equal(4, reconciliation.Value.MissingWorkIds.Count);
            Assert.Equal(materialized ? 4 : 0, reconciliation.Value.ActualOccurrenceIds.Count);
            Assert.Empty(reconciliation.Value.DuplicateOccurrenceIds);
            Assert.Contains(reconciliation.Value.SourceBlockers, blocker => blocker.Contains("opening", StringComparison.Ordinal));
            Assert.Equal(operation.AcceptedAt, reconciliation.Value.RecordedThrough);
            var manifest = await fixture.Host.Content.ReadJsonAsync<SnapshotSourcePublicationManifest>(fixture.TenantId.ToString(),
                reconciliation.Value.SourceContentManifest!);
            Assert.Equal(operation.AcceptedAt, manifest.RecordedThrough);
            Assert.Equal(fixture.PeriodId, manifest.ClosePeriodId);
            Assert.Equal(request.CommandId, manifest.CloseOperationId);
            Assert.All(contributors, contributor => Assert.Equal(operation.AcceptedAt, contributor.RecordedThrough));
            Assert.All(contributors, contributor => Assert.Equal(fixture.PeriodEnd, contributor.EffectiveOn));
            Assert.Contains(manifest.Sources, item => item.SourceType == "type_ii_native_history" && item.Content.Bytes > 0);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ShouldRequireCurrentRestrictedSourceVisibilityGivenHistoricalChangeIncludedInClose()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var change = new SignificantChangeRules(fixture.TenantId, fixture.ProgramId, Uuid.CreateVersion4());
        Assert.Null(change.Record(new SignificantChangeContent(null, null, null, "Restricted change", "Confidential details",
            fixture.PeriodStart, null, fixture.ProposerMemberId, [Uuid.CreateVersion4()], "high", "restricted", []),
            fixture.ProposerMemberId, "Recorder", StartedAt));
        await ChangeSeeds.WriteAsync(fixture.Writer, fixture.Host.Content, change);
        var period = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);
        var reader = new TypeIIPeriodCloseHistory(fixture.Events,
            new RestrictedComplianceRecordVisibility(new RecordingPermissionAuthorizer(false)),
            new SignificantChangeReader(fixture.Reader, fixture.Events, fixture.Host.Content, fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>()), fixture.Host.Content, new ComplianceIncidentReader(fixture.Reader, fixture.Events, fixture.Host.Content, fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>()), fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>());
        var request = await CloseRequestAsync(fixture);

        // Act
        var failure = await Record.ExceptionAsync(() => reader.ReadAsync(period.View!, fixture.Context(request).Actor,
            fixture.CloseClock.GetUtcNow(), CancellationToken.None));

        // Assert
        Assert.IsType<CloseSourceAccessException>(failure);
        Assert.Null(period.View!.CloseValidation);
    }

    static async Task SeedCloseEngagementAsync(Fixture fixture, Uuid boundaryId)
    {
        var engagement = new ExaminationEngagementRules(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId);
        Assert.Null(engagement.Create(new ExaminationEngagementContent("type_i", "Audit Firm", fixture.PeriodStart, null,
            ["security", "availability"], boundaryId, []), fixture.ProposerMemberId, "Engagement manager", StartedAt.AddDays(-4)));
        await ExaminationSeeds.WriteAsync(fixture.Reader, fixture.Writer, fixture.Events, fixture.Host.Content, engagement);
    }

    // Explicit family-reader scaffolding: native occurrence/history reconciliation and physical publication above are production services.
    sealed class CloseFamily(string family, Uuid boundaryId) : ITypeIBaselineSourceContributor
    {
        public string Family => family;
        public DateTimeOffset? RecordedThrough { get; private set; }
        public DateOnly EffectiveOn { get; private set; }
        public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(ExaminationEngagementView engagement, CancellationToken ct) =>
            ReadAsync(engagement, new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);
        public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(ExaminationEngagementView engagement,
            TypeIBaselineSourceReadContext context, CancellationToken ct)
        {
            RecordedThrough = context.RecordedThrough;
            EffectiveOn = engagement.PeriodStart;
            var bytes = Encoding.UTF8.GetBytes("bdgrz.snapshot.source.close_test.v1\0" + family);
            var sha = Convert.ToHexStringLower(SnapshotSourceCapture.HashData(bytes));
            var record = family == "approved_boundary" ? boundaryId : Uuid.CreateVersion5(boundaryId, family);
            return ValueTask.FromResult(Result<TypeIBaselineSourceContribution>.Success(new(Family,
                [new TypeIBaselineSourceReference(family, record, Uuid.CreateVersion5(record, "version"), 1, sha)], [], [])));
        }
    }
}
