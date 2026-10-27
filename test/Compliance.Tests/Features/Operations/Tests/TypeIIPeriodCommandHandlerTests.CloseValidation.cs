using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Projections;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
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
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AuditPopulations.Events;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Handlers;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

public sealed partial class TypeIIPeriodCommandHandlerTests
{
    [Fact]
    public async Task ShouldFreezeNativeCloseReconciliationAndOriginalUnitResultGivenCompletedObservationInterval()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var sources = new CloseSources(fixture);
        var request = await CloseRequestAsync(fixture);

        // Act
        var result = await CloseHandler(fixture, sources).HandleAsync(fixture.Context(request), CancellationToken.None);
        var owner = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId, fixture.ProgramId, fixture.PeriodId, request.CommandId));
        var accepted = Assert.IsType<ObservationPeriodCloseProposed>(owner.Acceptance.SelectedFact);
        var change = await fixture.Host.Content.ReadJsonAsync<TypeIIPeriodCloseValidationChange>(fixture.TenantId.ToString(), accepted.Change);
        var original = await fixture.Host.Content.ReadJsonAsync<MutationResultContent>(fixture.TenantId.ToString(), accepted.OperationResult!);
        var response = await FirstResponses.ReplayAsync(fixture.Host.Content, fixture.TenantId.ToString(), accepted.FirstResponse!);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, sources.Calls);
        Assert.Equal(request.CommandId, accepted.Operation.LogicalId);
        Assert.Equal((ulong)request.ExpectedRevision, accepted.Operation.OriginalExpectedRevision);
        Assert.Equal(fixture.CloseClock.GetUtcNow(), change.Header.AcceptedAt);
        Assert.Equal(change.Header.AcceptedAt, change.Content.RecordedThrough);
        Assert.Equal(UnitResult.Schema, original.Schema);
        Assert.IsType<StandardResponseBody>(original.Value);
        Assert.Equal(200, response.HttpStatus);
        Assert.Equal(UnitResult.Schema, response.ResultSchema);
        Assert.Equal("close_ready", owner.Header!.Status);
    }

    [Fact]
    public async Task ShouldReplayOriginalCloseWithoutSourceReadGivenClockAndLiveSourcesChangedAfterAcceptance()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var sources = new CloseSources(fixture);
        var request = await CloseRequestAsync(fixture);
        var handler = CloseHandler(fixture, sources);
        Assert.True((await handler.HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);
        var before = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);
        fixture.CloseClock.Set(fixture.CloseClock.GetUtcNow().AddDays(3));
        sources.Failure = new ImmutableContentIntegrityException("The live sources changed after acceptance.");

        // Act
        var replay = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var after = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(1, sources.Calls);
        Assert.Equal(before.Owner.CommittedStreamPosition, after.Owner.CommittedStreamPosition);
        Assert.Equal(before.View!.CloseValidation!.ValidatedAt, after.View!.CloseValidation!.ValidatedAt);
        Assert.Equal(before.View.CloseValidation.RecordedThrough, after.View.CloseValidation.RecordedThrough);
    }

    [Fact]
    public async Task ShouldRejectChangedExpectedRevisionGivenAcceptedCloseCommandIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var sources = new CloseSources(fixture);
        var request = await CloseRequestAsync(fixture);
        var handler = CloseHandler(fixture, sources);
        Assert.True((await handler.HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);

        // Act
        var changed = await handler.HandleAsync(fixture.Context(request with { ExpectedRevision = request.ExpectedRevision + 1 }), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(1, sources.Calls);
        var owner = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);
        Assert.Equal(request.ExpectedRevision + 1, owner.Revision);
    }

    [Fact]
    public async Task ShouldRequirePersonalHttpBeforeSourceReadGivenDirectCloseInvocation()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var sources = new CloseSources(fixture);
        var request = await CloseRequestAsync(fixture);
        var personal = fixture.Context(request);
        var direct = new Cntryl.Portia.RequestContext<RecordTypeIIPeriodCloseValidation>(request, new RequestDispatchContext(personal.Actor));

        // Act
        var result = await CloseHandler(fixture, sources).HandleAsync(direct, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(0, sources.Calls);
    }

    [Fact]
    public async Task ShouldRejectPrematureCloseBeforeSourceReadGivenObservationIntervalStillRunning()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        fixture.CloseClock.Set(StartedAt.AddDays(1));
        var sources = new CloseSources(fixture);
        var request = await CloseRequestAsync(fixture);

        // Act
        var result = await CloseHandler(fixture, sources).HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, sources.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRetainNoCloseFactGivenMissingOrCorruptSourcePublication(bool unavailable)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var sources = new CloseSources(fixture)
        {
            Failure = unavailable ? new ImmutableContentUnavailableException("Missing source.") : new ImmutableContentIntegrityException("Corrupt source."),
        };
        var request = await CloseRequestAsync(fixture);

        // Act
        var result = await CloseHandler(fixture, sources).HandleAsync(fixture.Context(request), CancellationToken.None);
        var period = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error?.IsTransient);
        Assert.Null(period.View!.CloseValidation);
        Assert.Equal(request.ExpectedRevision, period.Revision);
    }

    [Fact]
    public async Task ShouldKeepCloseBlockedGivenIncompleteSourceFamilyEvenWithCompleteOccurrenceIds()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var sources = new CloseSources(fixture) { Blockers = ["The complete source family is unavailable."] };
        var request = await CloseRequestAsync(fixture);

        // Act
        var result = await CloseHandler(fixture, sources).HandleAsync(fixture.Context(request), CancellationToken.None);
        var owner = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("close_blocked", owner.View!.Status);
        Assert.Equal("blocked", owner.View.CloseValidation!.Status);
        Assert.Single(owner.View.CloseValidation.SourceBlockers);
        Assert.Empty(owner.View.CloseApprovals);
    }

    [Fact]
    public async Task ShouldRejectForeignPublicationScopeGivenOtherwiseValidCloseReconciliation()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var sources = new CloseSources(fixture) { WrongOperation = true };
        var request = await CloseRequestAsync(fixture);

        // Act
        var result = await CloseHandler(fixture, sources).HandleAsync(fixture.Context(request), CancellationToken.None);
        var owner = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error?.IsTransient);
        Assert.Null(owner.View!.CloseValidation);
    }

    [Fact]
    public async Task ShouldRecoverOriginalCloseGivenAppendCommittedBeforeLostResponse()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var sources = new CloseSources(fixture);
        var request = await CloseRequestAsync(fixture);
        var unknown = CloseHandler(fixture, sources, new CloseCommitThenTimeout(fixture.Executor));

        // Act
        await Assert.ThrowsAsync<TimeoutException>(async () => await unknown.HandleAsync(fixture.Context(request), CancellationToken.None));
        fixture.CloseClock.Set(fixture.CloseClock.GetUtcNow().AddDays(1));
        sources.Failure = new ImmutableContentUnavailableException("The source is no longer readable.");
        var replay = await CloseHandler(fixture, sources).HandleAsync(fixture.Context(request), CancellationToken.None);
        var owner = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(1, sources.Calls);
        Assert.Equal(request.ExpectedRevision + 1, owner.Revision);
    }

    [Fact]
    public async Task ShouldReplayConcurrentEqualWinnerGivenSourcePreparationFailureAfterFirstOwnerRead()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var request = await CloseRequestAsync(fixture);
        var winnerSources = new CloseSources(fixture);
        var winner = CloseHandler(fixture, winnerSources);
        var losingSources = new CloseSources(fixture)
        {
            BeforeRead = async () =>
            {
                Assert.True((await winner.HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);
                throw new ImmutableContentUnavailableException("The stale preparation dependency is unavailable.");
            },
        };

        // Act
        var replay = await CloseHandler(fixture, losingSources).HandleAsync(fixture.Context(request), CancellationToken.None);
        var owner = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(1, winnerSources.Calls);
        Assert.Equal(1, losingSources.Calls);
        Assert.Equal(request.ExpectedRevision + 1, owner.Revision);
    }

    static RecordTypeIIPeriodCloseValidationHandler CloseHandler(Fixture fixture, ITypeIIPeriodCloseReconciliation source,
        IAggregateExecutor? executor = null) => new(new TypeIIPeriodOperations(
        new NativeOperationExecutor(fixture.Reader, executor ?? fixture.Executor, fixture.Host.Content), fixture.Periods,
        fixture.Host.Content, fixture.CloseClock), source, fixture.Host.Content);

    static async Task<RecordTypeIIPeriodCloseValidation> CloseRequestAsync(Fixture fixture)
    {
        var period = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);
        return new(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.PeriodId, period.Revision);
    }

    sealed class CloseSources(Fixture fixture) : ITypeIIPeriodCloseReconciliation
    {
        public int Calls { get; private set; }
        public Exception? Failure { get; set; }
        public Func<Task>? BeforeRead { get; init; }
        public bool WrongOperation { get; init; }
        public IReadOnlyList<string> Blockers { get; init; } = [];
        public async ValueTask<Result<TypeIIPeriodCloseValidationContent>> ReadAsync(TypeIIPeriodReadSnapshot period, ClaimsPrincipal actor,
            Uuid requesterUserId, OperationProvenance operation, CancellationToken ct)
        {
            Calls++;
            if (BeforeRead is { } before)
                await before();
            if (Failure is { } failure)
                throw failure;
            var current = period.View!;
            var source = new ExaminationContentReference("close_test_source", period.Id, Uuid.CreateVersion5(period.Id, "close-source"),
                period.Revision, ContentDigest.Sha256("{}"u8));
            var publication = await fixture.Host.Content.WriteJsonAsync(fixture.TenantId.ToString(), Uuid.CreateVersion4(),
                SnapshotSourcePublicationManifest.Schema, new SnapshotSourcePublicationManifest(fixture.TenantId, fixture.ProgramId,
                    fixture.ExaminationId, [new(source.SourceType, source.RecordId, source.VersionId, source.Version, source.ContentSha256,
                        new SnapshotSourceBlob(source.ContentSha256, 2, "application/json", "json", "Exact test source."))])
                {
                    ClosePeriodId = period.Id,
                    CloseOperationId = WrongOperation ? Uuid.CreateVersion4() : operation.RootOperationId,
                    RecordedThrough = operation.AcceptedAt,
                    SourceCoverage = TypeIBaselineSourceFamilies.Required.Select(family => TypeIBaselineContentIdentity.Coverage(family, [])).ToArray(),
                }, ct);
            var expected = current.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray();
            return Result<TypeIIPeriodCloseValidationContent>.Success(new([source], expected, expected, [], [], [])
            { SourceContentManifest = publication, RecordedThrough = operation.AcceptedAt, SourceBlockers = Blockers });
        }
    }

    sealed class CloseCommitThenTimeout(IAggregateExecutor inner) : IAggregateExecutor
    {
        public ValueTask<Result> ExecuteAsync<T>(T aggregate, Func<T, AggregateOutcome> operation, IExecutionContext context,
            CancellationToken ct) where T : Aggregate => inner.ExecuteAsync(aggregate, operation, context, ct);
        public async ValueTask<Result<TOut>> ExecuteAsync<T, TOut>(T aggregate, Func<T, AggregateOutcome<TOut>> operation,
            IExecutionContext context, CancellationToken ct) where T : Aggregate
        {
            _ = await inner.ExecuteAsync(aggregate, operation, context, ct);
            throw new TimeoutException("The native append response was lost after commit.");
        }
    }
}
