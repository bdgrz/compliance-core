using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Handlers;
using Bdgrz.Compliance.Features.Processes.Processes;
namespace Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;

public sealed class AuditorRequestLifecycleTests
{
    [Fact]
    public async Task ShouldSealAdmissionAndRefuseSecondResolutionGivenPendingResolution()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var id = await fixture.SeedRequestAsync();
        Assert.True((await fixture.DraftAsync(id, Uuid.CreateVersion4())).IsSuccess);
        var (accepted, _) = await fixture.ResolveAsync(id);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        var pending = await fixture.RequestAsync(id);
        var comment = new RecordAuditorRequestComment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, id,
            "Late discussion.", "internal", pending.Revision);
        var revise = new ReviseAuditorRequest(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, id,
            pending.Revision, "Late wording", "Late scope", "normal", new DateOnly(2026, 11, 1), "Late edit");

        // Act
        var admitted = await fixture.Handler<RecordAuditorRequestCommentHandler>().HandleAsync(fixture.Context(comment),
            CancellationToken.None);
        var revised = await fixture.Handler<ReviseAuditorRequestHandler>().HandleAsync(fixture.Context(revise),
            CancellationToken.None);
        var (second, _) = await fixture.ResolveAsync(id);
        var saved = await fixture.RequestAsync(id);

        // Assert
        Assert.NotNull(pending.PendingResolution);
        Assert.False(pending.IsOpen);
        Assert.Equal(RequestErrorKind.Conflict, admitted.Error?.Kind);
        Assert.True(admitted.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, revised.Error?.Kind);
        Assert.True(revised.Error!.IsTransient);
        Assert.False(second.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, second.Error!.Kind);
        Assert.True(second.Error.IsTransient);
        Assert.Equal(pending.Revision, saved.Revision);
        Assert.NotNull(saved.PendingResolution);
    }

    [Fact]
    public async Task ShouldRefuseWritesGivenResolvedRequestWithNoFactAppended()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var id = await ResolvedAsync(fixture);
        var resolved = await fixture.RequestAsync(id);
        var comment = new RecordAuditorRequestComment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, id,
            "Discussion after resolution.", "internal", resolved.Revision);
        var revise = new ReviseAuditorRequest(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, id,
            resolved.Revision, "Resolved wording", "Resolved scope", "normal", new DateOnly(2026, 11, 1), "Resolved edit");

        // Act
        var admitted = await fixture.Handler<RecordAuditorRequestCommentHandler>().HandleAsync(fixture.Context(comment),
            CancellationToken.None);
        var revised = await fixture.Handler<ReviseAuditorRequestHandler>().HandleAsync(fixture.Context(revise),
            CancellationToken.None);
        var saved = await fixture.RequestAsync(id);

        // Assert
        Assert.NotNull(resolved.Resolved);
        Assert.Equal(RequestErrorKind.Conflict, admitted.Error?.Kind);
        Assert.False(admitted.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, revised.Error?.Kind);
        Assert.False(revised.Error!.IsTransient);
        Assert.Equal(resolved.Revision, saved.Revision);
        Assert.NotNull(saved.Resolved);
    }

    [Fact]
    public async Task ShouldAdmitCommentInNextGenerationGivenReopenedRequest()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var id = await ResolvedAsync(fixture);
        var resolved = await fixture.RequestAsync(id);
        var reopen = new ReopenAuditorRequest(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, id,
            "Auditor asked a follow-up", resolved.Revision);
        Assert.True((await fixture.Handler<ReopenAuditorRequestHandler>().HandleAsync(fixture.Context(reopen),
            CancellationToken.None)).IsSuccess);
        var reopened = await fixture.RequestAsync(id);
        var comment = new RecordAuditorRequestComment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, id,
            "Follow-up discussion.", "internal", reopened.Revision);

        // Act
        var admitted = await fixture.Handler<RecordAuditorRequestCommentHandler>().HandleAsync(fixture.Context(comment),
            CancellationToken.None);
        var history = await fixture.RequestHistoryAsync(id);

        // Assert
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.True(reopened.IsOpen);
        Assert.Equal(2, reopened.Generation);
        Assert.Equal(resolved.Revision + 1, reopened.Revision);
        Assert.Equal(2, Assert.Single(history.Comments).Generation);
    }

    static async Task<Uuid> ResolvedAsync(AuditorRequestFixture fixture)
    {
        var id = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();
        Assert.True((await fixture.DraftAsync(id, responseId)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await fixture.MaterializeAsync(responseId)).State);
        Assert.True((await fixture.SubmitAsync(id, responseId)).IsSuccess);
        Assert.True((await fixture.ReviewAsync(id, responseId)).IsSuccess);
        Assert.True((await fixture.DeliverAsync(id, responseId)).IsSuccess);
        Assert.True((await fixture.AcceptAsync(id, responseId)).IsSuccess);
        var (accepted, operationId) = await fixture.ResolveAsync(id);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(ProcessState.Completed, (await fixture.FinishResolutionAsync(operationId)).State);
        return id;
    }
}
