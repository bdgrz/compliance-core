using System.Text;
using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AuditorRequests;

public sealed class AuditorRequestUnboundCancellationTests
{
    [Fact]
    public async Task ShouldKeepLaterResolutionAndResponseHeldGivenUnboundEarlierFailureProof()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();
        var draft = await fixture.DraftAsync(requestId, responseId);
        Assert.True(draft.IsSuccess, draft.Error?.Message);
        await fixture.MaterializeAsync(responseId);
        Assert.True((await fixture.SubmitAsync(requestId, responseId)).IsSuccess);
        Assert.True((await fixture.ReviewAsync(requestId, responseId)).IsSuccess);
        Assert.True((await fixture.DeliverAsync(requestId, responseId)).IsSuccess);
        Assert.True((await fixture.AcceptAsync(requestId, responseId)).IsSuccess);
        var first = await fixture.ResolveAsync(requestId);
        Assert.True(first.Result.IsSuccess, first.Result.Error?.Message);
        await fixture.FinishResolutionAsync(first.CommandId);
        var resolved = await fixture.RequestAsync(requestId);
        Assert.NotNull(resolved.Resolved);
        var reopen = new ReopenAuditorRequest(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            requestId, "A later generation needs its own authoritative resolution.", resolved.Revision);
        var reopened = await fixture.Handler<ReopenAuditorRequestHandler>().HandleAsync(fixture.Context(reopen),
            CancellationToken.None);
        Assert.True(reopened.IsSuccess, reopened.Error?.Message);
        var bytes = Encoding.UTF8.GetBytes($$"""
            {"program_id":"{{fixture.ProgramId}}","request_id":"{{requestId}}","reason":"An earlier source failed.","response_id":"{{responseId}}"}
            """);
        var failure = await fixture.Content.WriteVerifiedAsync(fixture.TenantId.ToString(), Uuid.CreateVersion4(),
            AuditorContent.FailureSchema, ImmutableContentStoreExtensions.JsonMediaType, bytes, CancellationToken.None);
        var later = await fixture.ResolveAsync(requestId);
        Assert.True(later.Result.IsSuccess, later.Result.Error?.Message);
        var owner = await fixture.RequestAsync(requestId);
        var accepted = owner.PendingResolution!;
        var source = NativeFactReference.Of(owner.Stream, accepted);
        var target = AuditorRequestResolutionEffects.ResponseKey(fixture.TenantId, fixture.ProgramId, requestId, responseId);
        var record = ProcessRecord.Start(ProcessKey.ForRoot(AuditorRequestResolutionEffects.ProcessName,
            later.CommandId, source), source, accepted.Operation.Episode, accepted.Intent, [target], AuditorRequestFixture.Now);
        var acquireId = AuditorRequestResolutionEffects.ReceiverCommandId(source.EventId,
            nameof(AuthorizeAuditorResponseRequestResolutionUse), target, accepted.Operation.Episode);
        var acquire = await AuditorRequestResolutionProcess.Definition().CreateReceiver(new ProcessStep(record,
            "acquire", nameof(AuthorizeAuditorResponseRequestResolutionUse), target, acquireId), fixture.Services,
            CancellationToken.None);
        var bus = fixture.Services.GetRequiredService<IRequestBus>();
        var acquired = await bus.DispatchAsync(acquire!, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(acquired.IsSuccess, acquired.Error?.Message);
        Assert.True((await fixture.ResponseAsync(requestId, responseId)).Uses.HasActiveUse);
        var command = new AbortAuditorRequestResolution(Uuid.CreateVersion4(), later.CommandId, owner.Stream,
            accepted.Generation, 0, failure);
        var before = owner.CommittedStreamPosition;

        // Act
        var refused = await bus.DispatchAsync(command, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var after = await fixture.RequestAsync(requestId);
        var response = await fixture.ResponseAsync(requestId, responseId);

        // Assert
        Assert.False(refused.IsSuccess, "An unbound earlier failure proof must not cancel the later resolution.");
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.False(refused.Error!.IsTransient);
        Assert.Equal(before, after.CommittedStreamPosition);
        Assert.Equal(accepted.Metadata.EventId, after.PendingResolution!.Metadata.EventId);
        Assert.True(response.Uses.HasActiveUse);
    }
}
