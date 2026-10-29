using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using ClientExportAggregate = Bdgrz.Compliance.Features.Exports.Aggregates.ClientExport;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
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
using Bdgrz.Compliance.Tests.Features.Responsibilities.Tests;
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
using Bdgrz.Compliance.Features.Exports.Aggregates;
using Bdgrz.Compliance.Features.Exports.Authorizers;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Exports.Events;
using Bdgrz.Compliance.Features.Exports.Handlers;
using Bdgrz.Compliance.Features.Exports.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Exports.Tests;

public sealed class RecordClientExportBuildResultHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 18, 0, 0, TimeSpan.Zero);
    const string Worker = "reactor:ClientExportBuildExecutionV1";

    [Fact]
    public async Task ShouldPersistExactBuildResultAndNamedWorkerAttributionGivenPendingExportBuild()
    {
        // Arrange
        await using var fixture = new Fixture();
        var trigger = await fixture.SeedRequestedExportAsync();
        var request = fixture.Command(trigger, 1, "built");

        // Act
        var result = await fixture.DispatchAsync(request,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), trigger);
        var retry = await fixture.DispatchAsync(request,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), trigger);
        var saved = await fixture.Reader.HydrateAsync(new ClientExportAggregate(fixture.TenantId,
            fixture.ExportId));
        var buildEvent = await fixture.FindLatestBuiltAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal("built", saved.Current!.Status);
        var attempt = Assert.Single(saved.Current.BuildAttempts);
        Assert.Equal(Uuid.CreateVersion5(trigger, "client-export-build-attempt-v1:1"),
            attempt.AttemptId);
        Assert.Null(attempt.CompletedByMemberId);
        Assert.Equal(Worker, attempt.EffectiveCompletedBy.Id);
        Assert.Equal("system_process", attempt.EffectiveCompletedBy.Kind);
        Assert.Null(saved.Current.ActorMemberId);
        Assert.Equal(Worker, saved.Current.EffectiveActor.Id);
        Assert.Equal("bdgrz.system", buildEvent.Event.Metadata.Actor?.Issuer);
        Assert.Equal(Worker, buildEvent.Event.Metadata.Actor?.Subject);
    }

    [Fact]
    public async Task ShouldRejectNonWorkerMismatchedCauseAndUnpersistedTriggerGivenPendingExportBuild()
    {
        // Arrange
        await using var fixture = new Fixture();
        var trigger = await fixture.SeedRequestedExportAsync();
        var request = fixture.Command(trigger, 1, "built");

        // Act
        var nonWorker = await fixture.DispatchAsync(request,
            RequestActor.CreateSystem("reactor:OtherWorker", "bdgrz.system"), trigger);
        var nonDirect = await fixture.DispatchAsync(request,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), trigger,
            new HttpInvocation("POST", "/internal/export-build", "/internal/export-build", "test"));
        var wrongCause = await fixture.DispatchAsync(request,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), Uuid.CreateVersion4());
        var missingTrigger = Uuid.CreateVersion4();
        var fabricated = await fixture.DispatchAsync(fixture.Command(missingTrigger, 1, "built"),
            RequestActor.CreateSystem(Worker, "bdgrz.system"), missingTrigger);
        var wrongSnapshot = request with
        {
            Result = request.Result with { SourceSnapshotSha256 = Hash('d') },
        };
        var mismatchedSnapshot = await fixture.DispatchAsync(wrongSnapshot,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), trigger);
        var saved = await fixture.Reader.HydrateAsync(new ClientExportAggregate(fixture.TenantId,
            fixture.ExportId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, nonWorker.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, nonDirect.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, wrongCause.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, fabricated.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, mismatchedSnapshot.Error?.Kind);
        Assert.Equal("requested", saved.Current!.Status);
        Assert.Empty(saved.Current.BuildAttempts);
    }

    [Fact]
    public async Task ShouldBindRetryToFailureEventAndRejectLateFirstAttemptGivenFailedBuildAttempt()
    {
        // Arrange
        await using var fixture = new Fixture();
        var firstTrigger = await fixture.SeedRequestedExportAsync();
        var failed = fixture.Command(firstTrigger, 1, "failed");
        // Act
        var firstResult = await fixture.DispatchAsync(failed,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), firstTrigger);
        Assert.True(firstResult.IsSuccess, firstResult.Error?.Message);
        var failureEvent = await fixture.FindLatestFailureAsync();

        var retry = fixture.Command(failureEvent.Event.Metadata.EventId, 2, "built");
        var retryResult = await fixture.DispatchAsync(retry,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), failureEvent.Event.Metadata.EventId);
        var staleAttempt = await fixture.DispatchAsync(fixture.Command(firstTrigger, 2, "built"),
            RequestActor.CreateSystem(Worker, "bdgrz.system"), firstTrigger);
        var saved = await fixture.Reader.HydrateAsync(new ClientExportAggregate(fixture.TenantId,
            fixture.ExportId));

        // Assert
        Assert.True(retryResult.IsSuccess, retryResult.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, staleAttempt.Error?.Kind);
        Assert.Equal("built", saved.Current!.Status);
        Assert.Collection(saved.Current.BuildAttempts,
            attempt => Assert.Equal("failed", attempt.Outcome),
            attempt =>
            {
                Assert.Equal("built", attempt.Outcome);
                Assert.Equal(Uuid.CreateVersion5(failureEvent.Event.Metadata.EventId,
                    "client-export-build-attempt-v1:2"), attempt.AttemptId);
            });
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly IDomainEventReader _events;
        readonly IAggregateWriter _writer;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ExportId { get; } = Uuid.CreateVersion4();
        public Uuid SnapshotId { get; } = Uuid.CreateVersion4();
        public Uuid ArchiveId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IRequestBus Bus { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            var store = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IDomainEventReader>(store);
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
            services.AddPortia()
                .AddRequestHandler<RecordClientExportBuildResultHandler>()
                .AddRequestAuthorizer<RecordClientExportBuildResultAuthorizer>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            var scoped = _scope.ServiceProvider;
            Reader = scoped.GetRequiredService<IAggregateReader>();
            _writer = scoped.GetRequiredService<IAggregateWriter>();
            Bus = scoped.GetRequiredService<IRequestBus>();
            _events = scoped.GetRequiredService<IDomainEventReader>();
        }

        public async Task<Uuid> SeedRequestedExportAsync()
        {
            var export = new ClientExportAggregate(TenantId, ExportId);
            Assert.Null(export.Request(new ClientExportContent("organization", "zip", "export-2026",
                    SnapshotId, Hash('a'), 1,
                    [new("tenant_record", Uuid.CreateVersion4(), 1, Hash('b'), "included",
                        null, null, [])], []),
                RbacIds.Member(TenantId, Uuid.CreateVersion4()), "Tenant owner", Now.AddMinutes(-5)));
            await _writer.SaveAsync(export, new RequestDispatchContext(RequestActor.System));
            await foreach (var record in _events.ReadAsync(export.Stream, 0))
                if (record.Event is ClientExportRequested)
                    return record.Event.Metadata.EventId;
            throw new InvalidOperationException("The client export request event was not persisted.");
        }

        public RecordClientExportBuildResult Command(Uuid triggerEventId, int attemptNumber,
            string outcome)
        {
            var result = outcome == "built"
                ? new ClientExportBuildResultContent(
                    Uuid.CreateVersion5(triggerEventId,
                        $"client-export-build-attempt-v1:{attemptNumber}"), attemptNumber,
                    "built", ArchiveId, Hash('c'), null, Uuid.CreateVersion4(), 1, 0,
                    SnapshotId, Hash('a'))
                : new ClientExportBuildResultContent(
                    Uuid.CreateVersion5(triggerEventId,
                        $"client-export-build-attempt-v1:{attemptNumber}"), attemptNumber,
                    "failed", null, null, "archive_timeout", null, 0, 0, SnapshotId, Hash('a'));
            return new(TenantId, ExportId, attemptNumber == 1 ? 1 : 2,
                triggerEventId, result, Now);
        }

        public ValueTask<Result> DispatchAsync(RecordClientExportBuildResult request,
            ClaimsPrincipal actor, Uuid? causationId, RequestInvocation? invocation = null) => Bus.DispatchAsync(request,
            new RequestDispatchContext(actor, invocation ?? new DirectInvocation(),
                new RequestMetadata(Uuid.CreateVersion4(), Uuid.CreateVersion4(), causationId)));

        public async Task<DomainEventRecord> FindLatestFailureAsync()
        {
            DomainEventRecord? failure = null;
            await foreach (var record in _events.ReadAsync(new ClientExportAggregate(TenantId,
                               ExportId).Stream, 0))
                if (record.Event is ClientExportBuildFailed)
                    failure = record;
            return failure ?? throw new InvalidOperationException(
                "The client export build failure event was not persisted.");
        }

        public async Task<DomainEventRecord> FindLatestBuiltAsync()
        {
            await foreach (var record in _events.ReadAsync(new ClientExportAggregate(TenantId,
                               ExportId).Stream, 0))
                if (record.Event is ClientExportBuilt)
                    return record;
            throw new InvalidOperationException("The client export build event was not persisted.");
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    static string Hash(char value) => new(value, 64);
}
