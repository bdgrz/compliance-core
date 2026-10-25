using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditorRequests;

public sealed class AuditorResponseCommandHandlerTests
{
    [Fact]
    public async Task ShouldCreateReviseAndSubmitResponseGivenPersonalCommands()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();

        // Act
        var created = await fixture.DraftAsync(requestId, responseId, "Initial evidence explanation");
        var materialized = await fixture.MaterializeAsync(responseId);
        var revised = await fixture.Handler<ReviseAuditResponseDraftHandler>().HandleAsync(fixture.Context(
            new ReviseAuditResponseDraft(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, requestId,
                responseId, 1, "Revised evidence explanation", [])), CancellationToken.None);
        var submitted = await fixture.SubmitAsync(requestId, responseId);
        var saved = await fixture.ResponseAsync(requestId, responseId);
        var version = await fixture.ReadAsync<AuditorResponseVersionView>((await fixture.ResponseHistoryAsync(requestId, responseId)).OfType<AuditResponseDraftRevised>().Single().Change);

        // Assert
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Equal(ProcessState.Completed, materialized.State);
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        Assert.True(submitted.IsSuccess, submitted.Error?.Message);
        Assert.Equal(2, saved.BusinessVersion);
        Assert.Equal("submitted", saved.Status);
        Assert.Equal("Revised evidence explanation", version.Summary);
        Assert.Equal(2, (await fixture.RequestAsync(requestId)).Revision);
    }

    [Fact]
    public async Task ShouldRequireReadableAvailableEvidenceGivenResponseDraft()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var artifactId = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Malware);

        // Act
        var result = await fixture.DraftAsync(requestId, Uuid.CreateVersion4(), evidence: [artifactId]);
        var saved = await fixture.RequestAsync(requestId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty((await fixture.RequestHistoryAsync(requestId)).Responses);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldRequireExactArtifactReadGrantGivenResponseDraft()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture(allowArtifactRead: false);
        var requestId = await fixture.SeedRequestAsync();
        var artifactId = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Clean);

        // Act
        var result = await fixture.DraftAsync(requestId, Uuid.CreateVersion4(), evidence: [artifactId]);
        var saved = await fixture.RequestAsync(requestId);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty((await fixture.RequestHistoryAsync(requestId)).Responses);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldReviewDeliverAcceptAndResolveResponseGivenIndependentReviewer()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();
        Assert.True((await fixture.DraftAsync(requestId, responseId)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await fixture.MaterializeAsync(responseId)).State);
        Assert.True((await fixture.SubmitAsync(requestId, responseId)).IsSuccess);

        // Act
        var authorReview = await fixture.ReviewAsync(requestId, responseId, actor: fixture.UserId);
        var independentReview = await fixture.ReviewAsync(requestId, responseId);
        var delivered = await fixture.DeliverAsync(requestId, responseId);
        var disposition = await fixture.AcceptAsync(requestId, responseId);
        var (resolved, operationId) = await fixture.ResolveAsync(requestId);
        var completed = await fixture.FinishResolutionAsync(operationId);
        var saved = await fixture.RequestAsync(requestId);
        var response = await fixture.ResponseAsync(requestId, responseId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, authorReview.Error?.Kind);
        Assert.True(independentReview.IsSuccess, independentReview.Error?.Message);
        Assert.True(delivered.IsSuccess, delivered.Error?.Message);
        Assert.True(disposition.IsSuccess, disposition.Error?.Message);
        Assert.True(resolved.IsSuccess, resolved.Error?.Message);
        Assert.Equal(ProcessState.Completed, completed.State);
        Assert.NotNull(saved.Resolved);
        Assert.Equal("accepted", response.Disposition!.Outcome);
        Assert.False(response.Uses.HasActiveUse);
    }
}
