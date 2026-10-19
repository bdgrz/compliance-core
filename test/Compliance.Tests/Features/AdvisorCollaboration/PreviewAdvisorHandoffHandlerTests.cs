using System.Security.Claims;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class PreviewAdvisorHandoffHandlerTests
{
    static readonly DateTimeOffset CreatedAt = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldPreviewExactChangedAndNewContentAndListRemovedSourcesGivenEndedBaseline()
    {
        // Arrange
        await using var fixture = new Fixture();
        var controlId = Uuid.CreateVersion4();
        var oldControl = AdvisorRecordVersion.ControlDraft(controlId, Uuid.CreateVersion4(), 1);
        var newControl = AdvisorRecordVersion.ControlDraft(controlId, Uuid.CreateVersion4(), 2);
        var removedPolicy = AdvisorRecordVersion.PolicyDraft(Uuid.CreateVersion4(), 1);
        var newEvidence = AdvisorRecordVersion.EvidenceArtifact(Uuid.CreateVersion4(), 1, 1,
            new string('a', 64), Uuid.CreateVersion4());
        var baselineId = await fixture.SeedEndedSharedHandoffAsync([oldControl, removedPolicy]);
        var previewId = await fixture.SeedDraftAsync([newControl, newEvidence], baselineId);
        var sources = new RecordingSharedSourceReader();

        // Act
        var result = await new PreviewAdvisorHandoffHandler(fixture.Reader,
                new AcceptCurrentAdvisorRecordVersions(), sources, fixture.Clock)
            .HandleAsync(Context(new PreviewAdvisorHandoff(fixture.TenantId, fixture.ProgramId,
                previewId)), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(baselineId, result.Value.ComparedToHandoffId);
        Assert.Equal(["changed", "new"], result.Value.SelectedSources.Select(item => item.Change));
        Assert.Equal(oldControl, result.Value.SelectedSources[0].ComparedSource);
        Assert.Null(result.Value.SelectedSources[1].ComparedSource);
        Assert.Equal(new[] { newControl, newEvidence }, sources.RequestedSources);
        Assert.All(result.Value.SelectedSources, item =>
        {
            Assert.Equal(item.SelectedSource, item.Content.SelectedSource);
            Assert.Equal(previewId, item.Content.HandoffId);
        });
        Assert.Equal([removedPolicy], result.Value.RemovedSources);
    }

    [Fact]
    public async Task ShouldRejectPreviewGivenUnchangedVersionFromBaseline()
    {
        // Arrange
        await using var fixture = new Fixture();
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var baselineId = await fixture.SeedEndedSharedHandoffAsync([source]);
        var previewId = await fixture.SeedDraftAsync([source], baselineId);
        var sources = new RecordingSharedSourceReader();

        // Act
        var result = await new PreviewAdvisorHandoffHandler(fixture.Reader,
                new AcceptCurrentAdvisorRecordVersions(), sources, fixture.Clock)
            .HandleAsync(Context(new PreviewAdvisorHandoff(fixture.TenantId, fixture.ProgramId,
                previewId)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(sources.RequestedSources);
    }

    [Fact]
    public async Task ShouldNotPreviewSharedHandoffGivenNonDraftStatus()
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = await fixture.SeedEndedSharedHandoffAsync(
            [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())]);
        var sources = new RecordingSharedSourceReader();

        // Act
        var result = await new PreviewAdvisorHandoffHandler(fixture.Reader,
                new AcceptCurrentAdvisorRecordVersions(), sources, fixture.Clock)
            .HandleAsync(Context(new PreviewAdvisorHandoff(fixture.TenantId, fixture.ProgramId,
                handoffId)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(sources.RequestedSources);
    }

    [Fact]
    public async Task ShouldRejectPreviewGivenExpiredDraft()
    {
        // Arrange
        await using var fixture = new Fixture(CreatedAt.AddYears(1));
        var previewId = await fixture.SeedDraftAsync(
            [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())]);
        var sources = new RecordingSharedSourceReader();

        // Act
        var result = await new PreviewAdvisorHandoffHandler(fixture.Reader,
                new AcceptCurrentAdvisorRecordVersions(), sources, fixture.Clock)
            .HandleAsync(Context(new PreviewAdvisorHandoff(fixture.TenantId, fixture.ProgramId,
                previewId)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(sources.RequestedSources);
    }

    [Fact]
    public async Task ShouldRejectPreviewGivenDraftChangesWhileSourceContentIsRead()
    {
        // Arrange
        await using var fixture = new Fixture();
        var selected = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var previewId = await fixture.SeedDraftAsync([selected]);
        var replacement = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var sources = new RecordingSharedSourceReader(async (tenantId, programId, handoffId, _) =>
        {
            var changed = await fixture.Reader.HydrateAsync(new AdvisorHandoff(tenantId,
                programId, handoffId));
            Assert.True(changed.ReviseSelection(1, [replacement], "Scope changed during preview",
                fixture.Actor, CreatedAt.AddMinutes(1)).IsSuccess);
            await fixture.Writer.SaveAsync(changed,
                new RequestDispatchContext(RequestActor.System));
        });

        // Act
        var result = await new PreviewAdvisorHandoffHandler(fixture.Reader,
                new AcceptCurrentAdvisorRecordVersions(), sources, fixture.Clock)
            .HandleAsync(Context(new PreviewAdvisorHandoff(fixture.TenantId, fixture.ProgramId,
                previewId)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldPropagateStaleSourceVersionGivenDraftPreview()
    {
        // Arrange
        await using var fixture = new Fixture();
        var previewId = await fixture.SeedDraftAsync(
            [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())]);
        var sources = new RecordingSharedSourceReader();

        // Act
        var result = await new PreviewAdvisorHandoffHandler(fixture.Reader,
                new RejectCurrentAdvisorRecordVersions(), sources, fixture.Clock)
            .HandleAsync(Context(new PreviewAdvisorHandoff(fixture.TenantId, fixture.ProgramId,
                previewId)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(sources.RequestedSources);
    }

    static RequestContext<PreviewAdvisorHandoff> Context(PreviewAdvisorHandoff request) =>
        new(request, new ClaimsPrincipal());

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public ActorReference Actor => ActorReference.ForMember(
            RbacIds.Member(TenantId, UserId), "Compliance lead");
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }

        public TimeProvider Clock { get; }

        public Fixture(DateTimeOffset? now = null)
        {
            Clock = new FixedTimeProvider(now ?? CreatedAt.AddHours(1));
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
            });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        }

        public async Task<Uuid> SeedEndedSharedHandoffAsync(
            IReadOnlyList<AdvisorRecordVersion> selection)
        {
            var handoffId = Uuid.CreateVersion4();
            var handoff = new AdvisorHandoff(TenantId, ProgramId, handoffId);
            Assert.True(handoff.Create("Prior review", selection, new DateOnly(2026, 12, 31),
                Actor, CreatedAt).IsSuccess);
            Assert.True(handoff.MarkReadyForReview(1, Actor, CreatedAt.AddMinutes(1)).IsSuccess);
            Assert.True(handoff.Close("Prior review completed", Actor,
                CreatedAt.AddMinutes(2)).IsSuccess);
            await Writer.SaveAsync(handoff, new RequestDispatchContext(RequestActor.System));
            return handoffId;
        }

        public async Task<Uuid> SeedDraftAsync(IReadOnlyList<AdvisorRecordVersion> selection,
            Uuid? comparedToHandoffId = null)
        {
            var handoffId = Uuid.CreateVersion4();
            var handoff = new AdvisorHandoff(TenantId, ProgramId, handoffId);
            Assert.True(handoff.Create("Follow-up review", selection,
                new DateOnly(2026, 12, 31), Actor, CreatedAt, comparedToHandoffId).IsSuccess);
            await Writer.SaveAsync(handoff, new RequestDispatchContext(RequestActor.System));
            return handoffId;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class RecordingSharedSourceReader(
        Func<Uuid, Uuid, Uuid, AdvisorRecordVersion, Task>? onRead = null)
        : IAdvisorSharedSourceReader
    {
        public List<AdvisorRecordVersion> RequestedSources { get; } = [];

        public async ValueTask<Result<AdvisorSharedSourceView>> GetAsync(Uuid tenantId,
            Uuid programId, Uuid handoffId, AdvisorRecordVersion selectedSource,
            CancellationToken ct = default)
        {
            RequestedSources.Add(selectedSource);
            if (onRead is not null)
                await onRead(tenantId, programId, handoffId, selectedSource);
            return Result<AdvisorSharedSourceView>.Success(new AdvisorSharedSourceView(tenantId,
                programId, handoffId, selectedSource, null, null, null, null, null, null, null));
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    sealed class AcceptCurrentAdvisorRecordVersions : IAdvisorRecordVersionResolver
    {
        public ValueTask<Result> RequireCurrentAsync(Uuid tenantId, Uuid programId,
            IReadOnlyList<AdvisorRecordVersion> selection, CancellationToken ct = default) =>
            ValueTask.FromResult(Result.Success);
    }

    sealed class RejectCurrentAdvisorRecordVersions : IAdvisorRecordVersionResolver
    {
        public ValueTask<Result> RequireCurrentAsync(Uuid tenantId, Uuid programId,
            IReadOnlyList<AdvisorRecordVersion> selection, CancellationToken ct = default) =>
            ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A selected source version is no longer current.")));
    }
}
