using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Operations;

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
            var history = new TypeIIPeriodCloseHistory(fixture.Reader, fixture.Events,
                new RestrictedComplianceRecordVisibility(new RecordingPermissionAuthorizer(true)),
                new SignificantChangeReader(fixture.Reader, fixture.Events, fixture.Host.Content,
                    fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>()), fixture.Host.Content,
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
