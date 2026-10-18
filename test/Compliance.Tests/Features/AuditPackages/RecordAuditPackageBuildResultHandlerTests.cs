using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPackages;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using AuditPackageAggregate = Bdgrz.Compliance.Features.AuditPackages.AuditPackage;

namespace Bdgrz.Compliance.Tests.Features.AuditPackages;

public sealed class RecordAuditPackageBuildResultHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 16, 0, 0, TimeSpan.Zero);
    const string Worker = "reactor:AuditPackageBuildExecutionV1";

    [Fact]
    public async Task ShouldRecordAndRetryExactBuildResultGivenNamedWorker()
    {
        // Arrange
        await using var fixture = new Fixture();
        var pending = await fixture.StartBuildAsync();
        var request = new RecordAuditPackageBuildResult(fixture.TenantId,
            fixture.ProgramId, fixture.PackageId, pending.AttemptId, pending.RequestEventId,
            pending.Output, Now.AddHours(1));

        // Act
        var result = await fixture.DispatchAsync(request,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), pending.RequestEventId);
        var retry = await fixture.DispatchAsync(request,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), pending.RequestEventId);
        var saved = await fixture.Reader.HydrateAsync(new AuditPackageAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PackageId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        var version = Assert.Single(saved.Current!.Versions);
        Assert.Equal("built", saved.Current.Status);
        var attempt = Assert.Single(version.BuildAttempts);
        Assert.Equal("built", attempt.Status);
        Assert.Equal(fixture.ArchiveId, attempt.ArchiveArtifactId);
        Assert.Equal(pending.Output.ArchiveSha256, attempt.ArchiveSha256);
    }

    [Fact]
    public async Task ShouldRejectResultGivenNonWorkerOrMismatchedCausation()
    {
        // Arrange
        await using var fixture = new Fixture();
        var pending = await fixture.StartBuildAsync();
        var request = new RecordAuditPackageBuildResult(fixture.TenantId,
            fixture.ProgramId, fixture.PackageId, pending.AttemptId, pending.RequestEventId,
            pending.Output, Now.AddHours(1));

        // Act
        var nonWorker = await fixture.DispatchAsync(request,
            RequestActor.CreateSystem("reactor:OtherWorker", "bdgrz.system"), pending.RequestEventId);
        var wrongCause = await fixture.DispatchAsync(request,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), Uuid.CreateVersion4());
        var fabricatedEventId = Uuid.CreateVersion4();
        var fabricated = request with { BuildRequestEventId = fabricatedEventId };
        var missingEvent = await fixture.DispatchAsync(fabricated,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), fabricatedEventId);
        var saved = await fixture.Reader.HydrateAsync(new AuditPackageAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PackageId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, nonWorker.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, wrongCause.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, missingEvent.Error?.Kind);
        Assert.Equal("building", saved.Current!.Status);
        Assert.Equal("requested", Assert.Single(saved.Current.Versions[0].BuildAttempts).Status);
    }

    [Fact]
    public async Task ShouldRetainRetryableFailureGivenNamedWorker()
    {
        // Arrange
        await using var fixture = new Fixture();
        var pending = await fixture.StartBuildAsync();
        var failedOutput = new AuditPackageBuildOutput(null, null, null, null, null, null,
            "archive_store_timeout");
        var request = new RecordAuditPackageBuildResult(fixture.TenantId,
            fixture.ProgramId, fixture.PackageId, pending.AttemptId, pending.RequestEventId,
            failedOutput, Now.AddHours(1));

        // Act
        var result = await fixture.DispatchAsync(request,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), pending.RequestEventId);
        var saved = await fixture.Reader.HydrateAsync(new AuditPackageAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PackageId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("build_failed", saved.Current!.Status);
        var attempt = Assert.Single(saved.Current.Versions[0].BuildAttempts);
        Assert.Equal("failed", attempt.Status);
        Assert.Equal("archive_store_timeout", attempt.FailureCode);
        Assert.Null(attempt.ArchiveArtifactId);
    }

    [Fact]
    public async Task ShouldRejectLateResultGivenDifferentCurrentAttempt()
    {
        // Arrange
        await using var fixture = new Fixture();
        var first = await fixture.StartBuildAsync();
        var failedOutput = first.Output with { FailureCode = "archive_store_timeout" };
        var failedPackage = await fixture.Reader.HydrateAsync(new AuditPackageAggregate(
            fixture.TenantId, fixture.ProgramId, fixture.PackageId));
        Assert.Null(failedPackage.RecordBuildResult(failedPackage.Revision, first.AttemptId,
            failedOutput, null, "build-worker", Now.AddMinutes(2)));
        await fixture.Writer.SaveAsync(failedPackage, new RequestDispatchContext(RequestActor.System));
        var second = await fixture.StartRetryAsync();
        var late = new RecordAuditPackageBuildResult(fixture.TenantId,
            fixture.ProgramId, fixture.PackageId, first.AttemptId, first.RequestEventId,
            first.Output, Now.AddMinutes(3));

        // Act
        var result = await fixture.DispatchAsync(late,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), first.RequestEventId);
        var saved = await fixture.Reader.HydrateAsync(new AuditPackageAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PackageId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("building", saved.Current!.Status);
        Assert.Equal(second.AttemptId, saved.Current.Versions[0].BuildAttempts[^1].BuildAttemptId);
        Assert.Equal("requested", saved.Current.Versions[0].BuildAttempts[^1].Status);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly IDomainEventReader _events;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid BaselineId { get; } = Uuid.CreateVersion4();
        public Uuid PackageId { get; } = Uuid.CreateVersion4();
        public Uuid ArchiveId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }
        public IRequestBus Bus { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            var store = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IDomainEventReader>(store);
            services.AddPortia()
                .AddRequestHandler<RecordAuditPackageBuildResultHandler>()
                .AddRequestAuthorizer<RecordAuditPackageBuildResultAuthorizer>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            var scoped = _scope.ServiceProvider;
            Reader = scoped.GetRequiredService<IAggregateReader>();
            Writer = scoped.GetRequiredService<IAggregateWriter>();
            Executor = scoped.GetRequiredService<IAggregateExecutor>();
            Bus = scoped.GetRequiredService<IRequestBus>();
            _events = scoped.GetRequiredService<IDomainEventReader>();
        }

        public ValueTask<Result> DispatchAsync(RecordAuditPackageBuildResult request,
            System.Security.Claims.ClaimsPrincipal actor, Uuid? causationId) =>
            Bus.DispatchAsync(request, new RequestDispatchContext(actor, new DirectInvocation(),
                new RequestMetadata(Uuid.CreateVersion4(), Uuid.CreateVersion4(), causationId)));

        public async Task<PendingBuild> StartBuildAsync()
        {
            var package = new AuditPackageAggregate(TenantId, ProgramId, PackageId);
            var actor = RbacIds.Member(TenantId, Uuid.CreateVersion4());
            Assert.Null(package.Create(Content(), actor, "Package author", Now));
            Assert.Null(package.RequestBuild(package.Revision, "build-1", actor,
                "Package author", Now.AddMinutes(1)));
            await Writer.SaveAsync(package, new RequestDispatchContext(RequestActor.System));
            var saved = await Reader.HydrateAsync(new AuditPackageAggregate(TenantId, ProgramId,
                PackageId));
            var attempt = Assert.Single(saved.Current!.Versions[0].BuildAttempts);
            var requestEvent = await FindBuildRequestedEventAsync(attempt.BuildAttemptId);
            var output = new AuditPackageBuildOutput(saved.Current.Versions[0].Manifest.AuditPackageManifestId,
                saved.Current.Versions[0].Manifest.ManifestSha256,
                saved.Current.Versions[0].Manifest.Entries, ArchiveId, new string('f', 64), 4096, null);
            return new(attempt.BuildAttemptId, requestEvent.Event.Metadata.EventId, output);
        }

        public async Task<PendingBuild> StartRetryAsync()
        {
            var current = await Reader.HydrateAsync(new AuditPackageAggregate(TenantId, ProgramId,
                PackageId));
            Assert.Null(current.RequestBuild(current.Revision, "build-2",
                RbacIds.Member(TenantId, Uuid.CreateVersion4()), "Retry actor", Now.AddMinutes(4)));
            await Writer.SaveAsync(current, new RequestDispatchContext(RequestActor.System));
            var attempt = (await Reader.HydrateAsync(new AuditPackageAggregate(TenantId, ProgramId,
                PackageId))).Current!.Versions[0].BuildAttempts[^1];
            var requestEvent = await FindBuildRequestedEventAsync(attempt.BuildAttemptId);
            return new(attempt.BuildAttemptId, requestEvent.Event.Metadata.EventId,
                new AuditPackageBuildOutput(current.Current!.Versions[0].Manifest.AuditPackageManifestId,
                    current.Current.Versions[0].Manifest.ManifestSha256,
                    current.Current.Versions[0].Manifest.Entries, ArchiveId,
                    new string('f', 64), 4096, null));
        }

        async Task<DomainEventRecord> FindBuildRequestedEventAsync(Uuid attemptId)
        {
            await foreach (var record in _events.ReadAsync(new AuditPackageAggregate(TenantId,
                               ProgramId, PackageId).Stream, 0))
                if (record.Event is AuditPackageBuildRequested requested &&
                    requested.State.Versions[^1].BuildAttempts.Any(item =>
                        item.BuildAttemptId == attemptId))
                    return record;
            throw new InvalidOperationException("The package build request event was not persisted.");
        }

        AuditPackageContent Content() => new(ExaminationId, "type_i", "type_i_baseline",
            BaselineId, new string('a', 64), "Type I package", [
                new("control_matrix", "control-17", "control", Uuid.CreateVersion4(), 3,
                    "csv", new string('b', 64), 512, "product_calculated"),
            ]);

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed record PendingBuild(Uuid AttemptId, Uuid RequestEventId,
        AuditPackageBuildOutput Output);
}
