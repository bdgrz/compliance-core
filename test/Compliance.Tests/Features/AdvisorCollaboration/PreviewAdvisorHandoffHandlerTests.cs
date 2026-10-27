using Bdgrz.Compliance.Features.AdvisorCollaboration.Handlers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Queries;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class PreviewAdvisorHandoffHandlerTests
{
    [Fact]
    public async Task ShouldPreviewExactChangedAndNewContentAndListRemovedSourcesGivenEndedBaseline()
    {
        // Arrange
        await using var fixture = new PreviewFixture();
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
        var result = await new PreviewAdvisorHandoffHandler(fixture.Host.Handoffs,
                new AcceptCurrentAdvisorRecordVersions(), sources, fixture.Host.Clock)
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
        await using var fixture = new PreviewFixture();
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var baselineId = await fixture.SeedEndedSharedHandoffAsync([source]);
        var previewId = await fixture.SeedDraftAsync([source], baselineId);
        var sources = new RecordingSharedSourceReader();

        // Act
        var result = await new PreviewAdvisorHandoffHandler(fixture.Host.Handoffs,
                new AcceptCurrentAdvisorRecordVersions(), sources, fixture.Host.Clock)
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
        await using var fixture = new PreviewFixture();
        var handoffId = await fixture.SeedEndedSharedHandoffAsync(
            [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())]);
        var sources = new RecordingSharedSourceReader();

        // Act
        var result = await new PreviewAdvisorHandoffHandler(fixture.Host.Handoffs,
                new AcceptCurrentAdvisorRecordVersions(), sources, fixture.Host.Clock)
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
        await using var fixture = new PreviewFixture();
        var previewId = await fixture.SeedDraftAsync(
            [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())]);
        fixture.Host.Clock.Advance(TimeSpan.FromDays(400));
        var sources = new RecordingSharedSourceReader();

        // Act
        var result = await new PreviewAdvisorHandoffHandler(fixture.Host.Handoffs,
                new AcceptCurrentAdvisorRecordVersions(), sources, fixture.Host.Clock)
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
        await using var fixture = new PreviewFixture();
        var selected = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var previewId = await fixture.SeedDraftAsync([selected]);
        var replacement = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var sources = new RecordingSharedSourceReader(async (_, _, handoffId, _) =>
            Assert.True((await fixture.Host.ReviseAsync(handoffId, 1, [replacement],
                "Scope changed during preview")).IsSuccess));

        // Act
        var result = await new PreviewAdvisorHandoffHandler(fixture.Host.Handoffs,
                new AcceptCurrentAdvisorRecordVersions(), sources, fixture.Host.Clock)
            .HandleAsync(Context(new PreviewAdvisorHandoff(fixture.TenantId, fixture.ProgramId,
                previewId)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldPropagateStaleSourceVersionGivenDraftPreview()
    {
        // Arrange
        await using var fixture = new PreviewFixture();
        var previewId = await fixture.SeedDraftAsync(
            [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())]);
        var sources = new RecordingSharedSourceReader();

        // Act
        var result = await new PreviewAdvisorHandoffHandler(fixture.Host.Handoffs,
                new RejectCurrentAdvisorRecordVersions(), sources, fixture.Host.Clock)
            .HandleAsync(Context(new PreviewAdvisorHandoff(fixture.TenantId, fixture.ProgramId,
                previewId)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(sources.RequestedSources);
    }

    static RequestContext<PreviewAdvisorHandoff> Context(PreviewAdvisorHandoff request) =>
        new(request, new ClaimsPrincipal());

    sealed class PreviewFixture : IAsyncDisposable
    {
        public AdvisorHost Host { get; } = new();

        public Uuid TenantId => Host.TenantId;

        public Uuid ProgramId => Host.ProgramId;

        public async Task<Uuid> SeedEndedSharedHandoffAsync(IReadOnlyList<AdvisorRecordVersion> selection)
        {
            var handoffId = Uuid.CreateVersion4();
            Assert.True((await Host.CreateAsync(handoffId, selection, "Prior review")).IsSuccess);
            Assert.True((await Host.ReadyAsync(handoffId, 1)).IsSuccess);
            Assert.True((await Host.EndAsync(handoffId, 2, revoke: false)).IsSuccess);
            return handoffId;
        }

        public async Task<Uuid> SeedDraftAsync(IReadOnlyList<AdvisorRecordVersion> selection,
            Uuid? comparedToHandoffId = null)
        {
            var handoffId = Uuid.CreateVersion4();
            await AdvisorSeed.HandoffAsync(Host.Provider, TenantId, ProgramId, handoffId, selection, Host.Lead,
                Host.Clock.GetUtcNow(), AdvisorHost.Expiry, ready: false, purpose: "Follow-up review",
                comparedTo: comparedToHandoffId);
            return handoffId;
        }

        public ValueTask DisposeAsync() => Host.DisposeAsync();
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
