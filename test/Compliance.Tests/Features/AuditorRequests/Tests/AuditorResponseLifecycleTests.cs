using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Handlers;
using Bdgrz.Compliance.Features.Processes.Processes;
namespace Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;

public sealed class AuditorResponseLifecycleTests
{
    [Fact]
    public async Task ShouldRefuseRevisionDeliveryAndSecondDispositionGivenDispositionRecorded()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();
        Assert.True((await fixture.DraftAsync(requestId, responseId)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await fixture.MaterializeAsync(responseId)).State);
        Assert.True((await fixture.SubmitAsync(requestId, responseId)).IsSuccess);
        Assert.True((await fixture.ReviewAsync(requestId, responseId)).IsSuccess);
        Assert.True((await fixture.DeliverAsync(requestId, responseId)).IsSuccess);
        Assert.True((await fixture.AcceptAsync(requestId, responseId)).IsSuccess);
        var owner = await fixture.ResponseAsync(requestId, responseId);

        // Act
        var revise = owner.PrepareRevise(owner.Revision);
        var deliver = owner.PrepareDeliver(owner.Revision, owner.BusinessVersion, false);
        var disposition = owner.PrepareDisposition(owner.Revision, owner.BusinessVersion);
        var second = await fixture.AcceptAsync(requestId, responseId);
        var saved = await fixture.ResponseAsync(requestId, responseId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, revise.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, deliver.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, disposition.Error?.Kind);
        Assert.False(second.IsSuccess);
        Assert.Equal(owner.Revision, saved.Revision);
        Assert.Equal("delivered", saved.Status);
    }

    [Fact]
    public async Task ShouldRefuseDispositionOfOtherVersionGivenDeliveredResponse()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();
        Assert.True((await fixture.DraftAsync(requestId, responseId)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await fixture.MaterializeAsync(responseId)).State);
        Assert.True((await fixture.SubmitAsync(requestId, responseId)).IsSuccess);
        Assert.True((await fixture.ReviewAsync(requestId, responseId)).IsSuccess);
        Assert.True((await fixture.DeliverAsync(requestId, responseId)).IsSuccess);
        var owner = await fixture.ResponseAsync(requestId, responseId);

        // Act
        var other = owner.PrepareDisposition(owner.Revision, owner.BusinessVersion + 1);
        var stale = owner.PrepareDisposition(owner.Revision - 1, owner.BusinessVersion);
        var exact = owner.PrepareDisposition(owner.Revision, owner.BusinessVersion);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, other.Error?.Kind);
        Assert.False(stale.IsSuccess);
        Assert.True(exact.IsSuccess, exact.Error?.Message);
    }

    [Fact]
    public async Task ShouldRequireApprovalGivenSubmittedResponseWhenInternalReviewRequired()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();
        Assert.True((await fixture.DraftAsync(requestId, responseId)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await fixture.MaterializeAsync(responseId)).State);
        Assert.True((await fixture.SubmitAsync(requestId, responseId)).IsSuccess);
        var owner = await fixture.ResponseAsync(requestId, responseId);

        // Act
        var unreviewed = owner.PrepareDeliver(owner.Revision, owner.BusinessVersion, true);
        var optional = owner.PrepareDeliver(owner.Revision, owner.BusinessVersion, false);
        var staleVersion = owner.PrepareDeliver(owner.Revision, owner.BusinessVersion + 1, false);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, unreviewed.Error?.Kind);
        Assert.True(optional.IsSuccess, optional.Error?.Message);
        Assert.False(staleVersion.IsSuccess);
    }

    [Fact]
    public async Task ShouldReviseIntoNextDraftVersionGivenRejectedReview()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();
        Assert.True((await fixture.DraftAsync(requestId, responseId)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await fixture.MaterializeAsync(responseId)).State);
        Assert.True((await fixture.SubmitAsync(requestId, responseId)).IsSuccess);
        Assert.True((await fixture.ReviewAsync(requestId, responseId, "rejected")).IsSuccess);
        var rejected = await fixture.ResponseAsync(requestId, responseId);

        // Act
        var deliverRejected = rejected.PrepareDeliver(rejected.Revision, rejected.BusinessVersion, false);
        var revised = await fixture.Handler<ReviseAuditResponseDraftHandler>().HandleAsync(fixture.Context(
            new ReviseAuditResponseDraft(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, requestId,
                responseId, rejected.Revision, "Addressed the review.", [])), CancellationToken.None);
        var saved = await fixture.ResponseAsync(requestId, responseId);

        // Assert
        Assert.Equal("rejected", rejected.Status);
        Assert.Equal(RequestErrorKind.Conflict, deliverRejected.Error?.Kind);
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        Assert.Equal("draft", saved.Status);
        Assert.Equal(rejected.BusinessVersion + 1, saved.BusinessVersion);
        Assert.Equal(rejected.Revision + 1, saved.Revision);
    }

    [Fact]
    public async Task ShouldReturnOriginalResultAndRefuseChangedIntentGivenSubmitRetriedAfterLaterFact()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();
        Assert.True((await fixture.DraftAsync(requestId, responseId)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await fixture.MaterializeAsync(responseId)).State);
        var commandId = Uuid.CreateVersion4();
        var submit = new SubmitAuditResponse(commandId, fixture.TenantId, fixture.ProgramId, requestId, responseId, 1,
            "Submitted exact response.", 1);
        var changed = new SubmitAuditResponse(commandId, fixture.TenantId, fixture.ProgramId, requestId, responseId, 1,
            "A different submission note.", 1);
        var handler = fixture.Handler<SubmitAuditResponseHandler>();
        Assert.True((await handler.HandleAsync(fixture.Context(submit), CancellationToken.None)).IsSuccess);
        Assert.True((await fixture.ReviewAsync(requestId, responseId)).IsSuccess);
        var before = await fixture.ResponseAsync(requestId, responseId);

        // Act
        var retry = await handler.HandleAsync(fixture.Context(submit), CancellationToken.None);
        var conflicting = await handler.HandleAsync(fixture.Context(changed), CancellationToken.None);
        var saved = await fixture.ResponseAsync(requestId, responseId);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.False(conflicting.IsSuccess);
        Assert.Equal(before.Revision, saved.Revision);
        Assert.Equal("approved", saved.Status);
    }
}
