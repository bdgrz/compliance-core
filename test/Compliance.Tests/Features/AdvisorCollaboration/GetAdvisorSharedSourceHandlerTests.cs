using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class GetAdvisorSharedSourceHandlerTests
{
    [Fact]
    public async Task ShouldReturnSelectedSourceGivenUnexpiredReadyHandoff()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var selected = AdvisorRecordVersion.PolicyVersion(Uuid.CreateVersion4(), 2, 4);
        var (handoffId, _) = await host.SeedReadyAsync(selected);
        var sources = new FixedSharedSourceReader();
        var request = new GetAdvisorSharedSource(host.TenantId, host.ProgramId, handoffId, selected.SourceFamily,
            selected.RecordId);

        // Act
        var result = await new GetAdvisorSharedSourceHandler(host.Handoffs, sources, host.Clock)
            .HandleAsync(new RequestContext<GetAdvisorSharedSource>(request, RequestActor.System),
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(selected, result.Value.SelectedSource);
        Assert.Equal(1, sources.Calls);
    }

    [Fact]
    public async Task ShouldNotReadSourceOutsideHandoffSelectionGivenKnownRecordIdentity()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var selected = AdvisorRecordVersion.PolicyVersion(Uuid.CreateVersion4(), 2, 4);
        var (handoffId, _) = await host.SeedReadyAsync(selected);
        var sources = new FixedSharedSourceReader();
        var request = new GetAdvisorSharedSource(host.TenantId, host.ProgramId, handoffId, selected.SourceFamily,
            Uuid.CreateVersion4());

        // Act
        var result = await new GetAdvisorSharedSourceHandler(host.Handoffs, sources, host.Clock)
            .HandleAsync(new RequestContext<GetAdvisorSharedSource>(request, RequestActor.System),
                CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(0, sources.Calls);
    }

    [Fact]
    public async Task ShouldNotReadSourceGivenExpiredHandoff()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var selected = AdvisorRecordVersion.PolicyVersion(Uuid.CreateVersion4(), 2, 4);
        var (handoffId, _) = await host.SeedReadyAsync(selected);
        host.Clock.Advance(TimeSpan.FromDays(400));
        var sources = new FixedSharedSourceReader();
        var request = new GetAdvisorSharedSource(host.TenantId, host.ProgramId, handoffId, selected.SourceFamily,
            selected.RecordId);

        // Act
        var result = await new GetAdvisorSharedSourceHandler(host.Handoffs, sources, host.Clock)
            .HandleAsync(new RequestContext<GetAdvisorSharedSource>(request, RequestActor.System),
                CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(0, sources.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldNotReturnSourceGivenHandoffEndedDuringSourceRead(bool revoke)
    {
        // Arrange
        await using var host = new AdvisorHost();
        var selected = AdvisorRecordVersion.PolicyVersion(Uuid.CreateVersion4(), 2, 4);
        var (handoffId, _) = await host.SeedReadyAsync(selected);
        var sources = new FixedSharedSourceReader(async _ =>
            Assert.True((await host.EndAsync(handoffId, 2, revoke)).IsSuccess));
        var request = new GetAdvisorSharedSource(host.TenantId, host.ProgramId, handoffId, selected.SourceFamily,
            selected.RecordId);

        // Act
        var result = await new GetAdvisorSharedSourceHandler(host.Handoffs, sources, host.Clock)
            .HandleAsync(new RequestContext<GetAdvisorSharedSource>(request, RequestActor.System),
                CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(1, sources.Calls);
    }

    sealed class FixedSharedSourceReader(Func<CancellationToken, Task>? onRead = null)
        : IAdvisorSharedSourceReader
    {
        public int Calls { get; private set; }

        public async ValueTask<Result<AdvisorSharedSourceView>> GetAsync(Uuid tenantId, Uuid programId,
            Uuid handoffId, AdvisorRecordVersion selectedSource, CancellationToken ct = default)
        {
            Calls++;
            if (onRead is not null)
                await onRead(ct);
            return Result<AdvisorSharedSourceView>.Success(new AdvisorSharedSourceView(tenantId,
                programId, handoffId, selectedSource, null, null, null, null, null, null, null));
        }
    }
}
