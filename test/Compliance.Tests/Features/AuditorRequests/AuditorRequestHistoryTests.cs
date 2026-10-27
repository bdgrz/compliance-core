using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Handlers;
using Bdgrz.Compliance.Features.AuditorRequests;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditorRequests;

public sealed class AuditorRequestHistoryTests
{
    [Fact]
    public async Task ShouldRejectStaleHistoryAdmissionCertificateGivenLaterRequestFact()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var id = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();
        Assert.True((await fixture.DraftAsync(id, responseId)).IsSuccess);
        var stale = await fixture.RequestHistoryAsync(id);
        var owner = await fixture.RequestAsync(id);
        var comment = new RecordAuditorRequestComment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            id, "A later admission changes the exact root cut.", "internal", owner.Revision);
        Assert.True((await fixture.Handler<RecordAuditorRequestCommentHandler>().HandleAsync(fixture.Context(comment),
            CancellationToken.None)).IsSuccess);
        var fresh = await fixture.RequestAsync(id);
        var history = await fixture.RequestHistoryAsync(id);

        // Act
        var refused = fresh.PrepareResolution(fresh.Revision, [responseId], stale);
        var exact = fresh.PrepareResolution(fresh.Revision, [responseId], history);
        var omitted = fresh.PrepareResolution(fresh.Revision, [], history);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.True(refused.Error!.IsTransient);
        Assert.True(exact.IsSuccess, exact.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, omitted.Error?.Kind);
        Assert.False(omitted.Error!.IsTransient);
        Assert.Single(history.Comments);
        Assert.Single(history.Responses);
        Assert.Equal(3, history.Facts.Count);
    }
}
