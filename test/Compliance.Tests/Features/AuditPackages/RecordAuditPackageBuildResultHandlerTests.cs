using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditPackages;

public sealed class RecordAuditPackageBuildResultHandlerTests
{
    [Fact]
    public async Task ShouldRecordAndRetryExactBuildResultGivenNamedWorker()
    {
        // Arrange
        await using var fixture = new Fixture();
        var start = await fixture.StartBuildAsync();
        Assert.True((await fixture.StartAsync(start)).IsSuccess);
        var command = await fixture.ResultAsync(start);

        // Act
        var first = await fixture.RecordAsync(command);
        var retry = await fixture.RecordAsync(command);
        var saved = await fixture.Inner.ReadPackageAsync(fixture.PackageId);

        // Assert
        Assert.IsType<ReceiverOutcome.Applied>(first.Value);
        Assert.IsType<ReceiverOutcome.Replayed>(retry.Value);
        Assert.Equal("built", saved.Current!.Status);
        var owner = await fixture.OwnerAsync();
        var built = Assert.Single(owner.Facts.OfType<AuditPackageBuilt>());
        Assert.NotNull(built.OperationResult);
        var terminal = await fixture.Inner.ContentStore.ReadJsonAsync<MutationResultContent>(fixture.Inner.TenantId.ToString(),
            built.OperationResult);
        Assert.Equal(UnitResult.Schema, terminal.Schema);
        Assert.IsType<StandardResponseBody>(terminal.Value);
        Assert.Equal(3, owner.Revision);
    }

    [Fact]
    public async Task ShouldRejectResultGivenNonWorkerOrMismatchedCausation()
    {
        // Arrange
        await using var fixture = new Fixture();
        var first = await fixture.StartBuildAsync();
        Assert.True((await fixture.StartAsync(first)).IsSuccess);
        var command = await fixture.ResultAsync(first);
        var newer = await fixture.StartBuildAsync("another-build");
        var personal = AmendAuditPackageHandlerTests.Fixture.Context<IAuditPackageReceiver>(fixture.Inner.UserId, command);

        // Act
        var forbidden = await new AuditPackageReceiverAuthorizer().AuthorizeAsync(personal, CancellationToken.None);
        var mismatched = await fixture.RecordAsync(command with { Source = newer.Source });

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, forbidden.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, mismatched.Error?.Kind);
        Assert.False(mismatched.Error!.IsTransient);
        Assert.Empty((await fixture.OwnerAsync()).Facts.OfType<AuditPackageBuilt>());
    }

    [Fact]
    public async Task ShouldRetainRetryableFailureGivenNamedWorker()
    {
        // Arrange
        await using var fixture = new Fixture();
        var start = await fixture.StartBuildAsync(storeSources: false);

        // Act
        var absent = await fixture.StartAsync(start);
        var pendingOutput = await fixture.Outputs.ReadAsync(fixture.Inner.TenantId, start.BuildId, CancellationToken.None);
        var before = await fixture.OwnerAsync();
        await fixture.Inner.SeedSourceBytesAsync();
        var resumed = await fixture.StartAsync(start);
        var committed = await fixture.RecordAsync(await fixture.ResultAsync(start));

        // Assert
        Assert.True(absent.Error!.IsTransient);
        Assert.Null(pendingOutput);
        Assert.Empty(before.Facts.OfType<AuditPackageBuilt>());
        Assert.Equal(2, before.Revision);
        Assert.IsType<ReceiverOutcome.Performed>(resumed.Value);
        Assert.IsType<ReceiverOutcome.Applied>(committed.Value);
        Assert.Equal(3, (await fixture.OwnerAsync()).Revision);
    }

    [Fact]
    public async Task ShouldRejectLateResultGivenDifferentCurrentAttempt()
    {
        // Arrange
        await using var fixture = new Fixture();
        var first = await fixture.StartBuildAsync();
        Assert.True((await fixture.StartAsync(first)).IsSuccess);
        var command = await fixture.ResultAsync(first);
        var newer = await fixture.StartBuildAsync("retry-build");

        // Act
        var result = await fixture.RecordAsync(command with { BuildId = newer.BuildId, OperationId = newer.BuildId });

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error!.IsTransient);
        Assert.Empty((await fixture.OwnerAsync()).Facts.OfType<AuditPackageBuilt>());
    }

    [Fact]
    public async Task ShouldRecordExactHistoricalBuildGivenLaterRequestOnSameUnfrozenManifest()
    {
        // Arrange
        await using var fixture = new Fixture();
        var first = await fixture.StartBuildAsync();
        Assert.True((await fixture.StartAsync(first)).IsSuccess);
        var command = await fixture.ResultAsync(first);
        await fixture.StartBuildAsync("later-build");

        // Act
        var result = await fixture.RecordAsync(command);

        // Assert
        Assert.IsType<ReceiverOutcome.Applied>(result.Value);
        Assert.Single((await fixture.OwnerAsync()).Facts.OfType<AuditPackageBuilt>());
    }

    [Fact]
    public async Task ShouldRejectPartialOutputGivenVerifiedCompleteArchiveCheckpoint()
    {
        // Arrange
        await using var fixture = new Fixture();
        var start = await fixture.StartBuildAsync();
        Assert.True((await fixture.StartAsync(start)).IsSuccess);
        var command = await fixture.ResultAsync(start);

        // Act
        var result = await fixture.RecordAsync(command with
        {
            Output = command.Output with { IncludedEntries = command.Output.IncludedEntries!.Take(1).ToArray() }
        });

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error!.IsTransient);
        Assert.Empty((await fixture.OwnerAsync()).Facts.OfType<AuditPackageBuilt>());
    }

    [Fact]
    public async Task ShouldReplaySuccessfulResultGivenArchiveUnavailableAfterAcceptedAppend()
    {
        // Arrange
        await using var fixture = new Fixture();
        var start = await fixture.StartBuildAsync();
        Assert.True((await fixture.StartAsync(start)).IsSuccess);
        var command = await fixture.ResultAsync(start);
        Assert.True((await fixture.RecordAsync(command)).IsSuccess);
        await fixture.Inner.ArtifactStore.QuarantineAsync(new Bdgrz.Compliance.Features.Artifacts.ArtifactContentReference(
            fixture.Inner.TenantId, command.Output.ArchiveSha256!, command.Output.ArchiveBytes!.Value));

        // Act
        var replay = await fixture.RecordAsync(command);

        // Assert
        Assert.IsType<ReceiverOutcome.Replayed>(replay.Value);
        Assert.Single((await fixture.OwnerAsync()).Facts.OfType<AuditPackageBuilt>());
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        static readonly DateTimeOffset Now = new(2026, 10, 18, 17, 0, 0, TimeSpan.Zero);
        public AmendAuditPackageHandlerTests.Fixture Inner { get; } = new();
        public Uuid PackageId { get; } = Uuid.CreateVersion4();
        public IAuditPackageBuildOutputs Outputs { get; } = new FitzKvAuditPackageBuildOutputs(
            new PersistenceKvClient(new InMemoryKvClient()));

        public async Task<StartAuditPackageBuild> StartBuildAsync(string key = "initial-build", bool storeSources = true)
        {
            var owner = await OwnerAsync();
            AuditPackageRules rules;
            if (!owner.IsCreated)
            {
                var baseline = await Inner.SeedBaselineAsync();
                rules = new AuditPackageRules(Inner.TenantId, Inner.ProgramId, PackageId);
                Assert.Null(rules.Create(Inner.Content(baseline), RbacIds.Member(Inner.TenantId, Inner.UserId), "Author", Now));
            }
            else
                rules = await Inner.ReadPackageAsync(PackageId);
            Assert.Null(rules.RequestBuild(rules.Revision, key, RbacIds.Member(Inner.TenantId, Inner.UserId), "Author",
                Now.AddMinutes(2)));
            await PackageSeeds.AppendAsync(Inner.Reader, Inner.Writer, Inner.ContentStore, rules.Current!);
            if (storeSources)
                await Inner.SeedSourceBytesAsync();
            owner = await OwnerAsync();
            var admitted = owner.Facts.OfType<AuditPackageBuildRequested>().Last();
            return new StartAuditPackageBuild(EffectIdentity.CommandId(admitted.Metadata.EventId, $"{AuditPackageBuildProcess.Name}:{nameof(StartAuditPackageBuild)}", owner.Stream.ToString(), admitted.Operation.Episode),
                NativeFactReference.Of(owner.Stream, admitted), owner.Stream, admitted.Operation.LogicalId, 1, null,
                admitted.Change, PackageId, admitted.Operation.LogicalId, 1);
        }

        public ValueTask<Result<ReceiverOutcome>> StartAsync(StartAuditPackageBuild command) =>
            new StartAuditPackageBuildHandler(new AuditPackageSourceVerifier(Inner.Reader, Inner.ContentStore),
                new AuditPackageArchiveBuilder(Inner.ArtifactStore), Outputs, Inner.ContentStore).HandleAsync(
                new Cntryl.Portia.RequestContext<StartAuditPackageBuild>(command,
                    new RequestDispatchContext(RequestActor.System)), CancellationToken.None);

        public async Task<RecordAuditPackageBuildResult> ResultAsync(StartAuditPackageBuild start)
        {
            var checkpoint = await Outputs.ReadAsync(Inner.TenantId, start.BuildId, CancellationToken.None);
            Assert.NotNull(checkpoint);
            var output = await Inner.ContentStore.ReadJsonAsync<AuditPackageBuildOutput>(Inner.TenantId.ToString(),
                checkpoint.VerifiedOutput);
            return new RecordAuditPackageBuildResult(EffectIdentity.CommandId(start.Source.EventId, $"{AuditPackageBuildProcess.Name}:{nameof(RecordAuditPackageBuildResult)}", start.Target.ToString(), "1"), start.Source,
                start.Target, start.OperationId, 1, null, start.Intent, start.PackageId, start.BuildId, output);
        }

        public ValueTask<Result<ReceiverOutcome>> RecordAsync(RecordAuditPackageBuildResult command) =>
            new RecordAuditPackageBuildResultHandler(new AuditPackageSourceVerifier(Inner.Reader, Inner.ContentStore),
                Inner.Reader, new ReceiverAppend(Inner.Executor), Outputs, Inner.ContentStore, Inner.ArtifactStore,
                TimeProvider.System).HandleAsync(new Cntryl.Portia.RequestContext<RecordAuditPackageBuildResult>(command,
                    new RequestDispatchContext(RequestActor.System)), CancellationToken.None);
        public ValueTask<AuditPackageManifest> OwnerAsync() =>
            Inner.Reader.HydrateAsync(new AuditPackageManifest(Inner.TenantId, PackageId));
        public ValueTask DisposeAsync() => Inner.DisposeAsync();
    }
}
