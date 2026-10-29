using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.AuditorRequests.Handlers;
using Bdgrz.Compliance.Features.AuditorRequests.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;

public sealed class AuditorRequestTests
{
    [Fact]
    public async Task ShouldRecordExactRequestAndAllowOnlyExactCreateRetryGivenCreate()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        await fixture.SeedMemberAsync(fixture.UserId);
        var command = fixture.NewRecordCommand();
        var handler = fixture.Handler<RecordAuditorRequestHandler>();

        // Act
        var created = await handler.HandleAsync(fixture.Context(command), CancellationToken.None);
        var retry = await handler.HandleAsync(fixture.Context(command), CancellationToken.None);
        var changed = await handler.HandleAsync(fixture.Context(command with { Wording = "Different request" }), CancellationToken.None);
        var request = await fixture.RequestAsync(command.RequestId);

        // Assert
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.True(request.IsOpen);
        Assert.Equal(1, request.Revision);
        Assert.Single((await fixture.RequestHistoryAsync(command.RequestId)).Facts);
        Assert.Equal(command.Wording, (await fixture.TermsAsync(command.RequestId)).Wording);
    }

    [Fact]
    public async Task ShouldRequireReviewBeforeDeliveryGivenRequiredReviewPolicy()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();
        Assert.True((await fixture.DraftAsync(requestId, responseId)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await fixture.MaterializeAsync(responseId)).State);
        Assert.True((await fixture.SubmitAsync(requestId, responseId)).IsSuccess);

        // Act
        var early = await fixture.DeliverAsync(requestId, responseId);
        var authorReview = await fixture.ReviewAsync(requestId, responseId, actor: fixture.UserId);
        var approved = await fixture.ReviewAsync(requestId, responseId);
        var delivered = await fixture.DeliverAsync(requestId, responseId);
        var response = await fixture.ResponseAsync(requestId, responseId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, early.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, authorReview.Error?.Kind);
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        Assert.True(delivered.IsSuccess, delivered.Error?.Message);
        Assert.Equal("delivered", response.Status);
        Assert.Equal(1, response.Delivery!.Version);
        Assert.Contains(await fixture.ResponseHistoryAsync(requestId, responseId), static fact => fact is AuditorResponseDelivered);
    }

    [Fact]
    public async Task ShouldPreserveRejectedVersionAndDeliverRevisedVersionGivenResponseRevision()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();
        Assert.True((await fixture.DraftAsync(requestId, responseId, "First draft")).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await fixture.MaterializeAsync(responseId)).State);
        Assert.True((await fixture.SubmitAsync(requestId, responseId)).IsSuccess);
        Assert.True((await fixture.ReviewAsync(requestId, responseId, "rejected")).IsSuccess);
        var revision = (await fixture.ResponseAsync(requestId, responseId)).Revision;

        // Act
        var revised = await fixture.Handler<ReviseAuditResponseDraftHandler>().HandleAsync(fixture.Context(
            new ReviseAuditResponseDraft(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, requestId,
                responseId, revision, "Revised with explanation", [])), CancellationToken.None);
        var submitted = await fixture.SubmitAsync(requestId, responseId);
        var approved = await fixture.ReviewAsync(requestId, responseId);
        var delivered = await fixture.DeliverAsync(requestId, responseId);
        var response = await fixture.ResponseAsync(requestId, responseId);

        // Assert
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        Assert.True(submitted.IsSuccess, submitted.Error?.Message);
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        Assert.True(delivered.IsSuccess, delivered.Error?.Message);
        Assert.Equal(2, response.BusinessVersion);
        Assert.Equal(2, response.Delivery!.Version);
        var reviews = (await fixture.ResponseHistoryAsync(requestId, responseId)).OfType<AuditorResponseDisclosureReviewed>().ToArray();
        Assert.Equal("rejected", reviews[0].Outcome);
        Assert.Equal(1, reviews[0].Version);
        Assert.Equal("approved", reviews[1].Outcome);
        Assert.Equal(2, reviews[1].Version);
    }

    [Fact]
    public async Task ShouldBindSampleSelectionToFrozenPopulationAndKeepItImmutableGivenSelection()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var (populationId, rowId, _, digest) = await fixture.SeedFrozenPopulationAsync();
        var command = SampleSelectionCommandHandlerTests.RecordCommand(fixture, requestId, populationId, rowId);
        var handler = fixture.Handler<RecordSampleSelectionHandler>();

        // Act
        var created = await handler.HandleAsync(fixture.Context(command), CancellationToken.None);
        var materialized = await fixture.MaterializeAsync(command.SelectionId);
        var retry = await handler.HandleAsync(fixture.Context(command), CancellationToken.None);
        var changed = await handler.HandleAsync(fixture.Context(command with { ExternalReference = "changed-reference" }), CancellationToken.None);
        var invalid = await handler.HandleAsync(fixture.Context(command with { SelectionId = Uuid.CreateVersion4(), ExpectedRequestRevision = 2, SelectionMethod = "unsupported" }), CancellationToken.None);
        var selection = await SampleSelectionCommandHandlerTests.SelectionAsync(fixture, requestId, command.SelectionId);

        // Assert
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Equal(ProcessState.Completed, materialized.State);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, invalid.Error?.Kind);
        Assert.Equal(populationId, selection.PopulationId);
        Assert.Equal(digest, (await fixture.ReadAsync<AuditPopulationVersionView>(selection.PopulationContent)).ContentSha256);
        Assert.Single((await fixture.RequestHistoryAsync(requestId)).Selections);
    }

    [Fact]
    public async Task ShouldRetainExactPopulationVersionAndSourceProvenanceGivenSampleAndAmendment()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var (populationId, rowId, _, firstDigest) = await fixture.SeedFrozenPopulationAsync();
        var first = SampleSelectionCommandHandlerTests.RecordCommand(fixture, requestId, populationId, rowId);
        Assert.True((await fixture.Handler<RecordSampleSelectionHandler>().HandleAsync(fixture.Context(first), CancellationToken.None)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await fixture.MaterializeAsync(first.SelectionId)).State);
        var secondDigest = await fixture.AmendPopulationAsync(populationId, rowId);
        var command = new AmendSampleSelection(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId, requestId,
            2, Uuid.CreateVersion4(), first.SelectionId, populationId, 2, [rowId], "targeted", "external_portal",
            "portal-export-119", "The auditor selected additional rows from the amended population.");

        // Act
        var sameVersion = await fixture.Handler<AmendSampleSelectionHandler>().HandleAsync(fixture.Context(command with { PopulationVersion = 1 }), CancellationToken.None);
        var amended = await fixture.Handler<AmendSampleSelectionHandler>().HandleAsync(fixture.Context(command), CancellationToken.None);
        var materialized = await fixture.MaterializeAsync(command.SelectionId);
        var original = await SampleSelectionCommandHandlerTests.SelectionAsync(fixture, requestId, first.SelectionId);
        var successor = await SampleSelectionCommandHandlerTests.SelectionAsync(fixture, requestId, command.SelectionId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, sameVersion.Error?.Kind);
        Assert.True(amended.IsSuccess, amended.Error?.Message);
        Assert.Equal(ProcessState.Completed, materialized.State);
        Assert.Equal(firstDigest, (await fixture.ReadAsync<AuditPopulationVersionView>(original.PopulationContent)).ContentSha256);
        Assert.Equal(secondDigest, (await fixture.ReadAsync<AuditPopulationVersionView>(successor.PopulationContent)).ContentSha256);
        Assert.Equal("external_email", original.Source);
        Assert.Equal("sample-mail-001", original.ExternalReference);
        Assert.Null(original.SupersedesSelectionId);
        Assert.Equal("external_portal", successor.Source);
        Assert.Equal("portal-export-119", successor.ExternalReference);
        Assert.Equal(first.SelectionId, successor.SupersedesSelectionId);
        Assert.Equal(command.AmendmentReason, successor.AmendmentReason);
    }

    [Fact]
    public async Task ShouldRetainRequestHistoryAcrossResolutionAndReopenGivenLifecycle()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var comment = new RecordAuditorRequestComment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            requestId, "Need clarification", "internal", 1);
        Assert.True((await fixture.Handler<RecordAuditorRequestCommentHandler>().HandleAsync(fixture.Context(comment), CancellationToken.None)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await fixture.MaterializeAsync(comment.CommentId)).State);
        var responseId = Uuid.CreateVersion4();
        Assert.True((await fixture.DraftAsync(requestId, responseId)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await fixture.MaterializeAsync(responseId)).State);
        Assert.True((await fixture.SubmitAsync(requestId, responseId)).IsSuccess);
        Assert.True((await fixture.ReviewAsync(requestId, responseId)).IsSuccess);
        Assert.True((await fixture.DeliverAsync(requestId, responseId)).IsSuccess);
        Assert.True((await fixture.AcceptAsync(requestId, responseId)).IsSuccess);
        var (accepted, operationId) = await fixture.ResolveAsync(requestId);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(ProcessState.Completed, (await fixture.FinishResolutionAsync(operationId)).State);
        var resolved = await fixture.RequestAsync(requestId);
        var command = new ReopenAuditorRequest(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, requestId,
            "Auditor asked a follow-up", resolved.Revision);

        // Act
        var stale = await fixture.Handler<ReopenAuditorRequestHandler>().HandleAsync(fixture.Context(command with { ExpectedRevision = command.ExpectedRevision - 1 }), CancellationToken.None);
        var reopened = await fixture.Handler<ReopenAuditorRequestHandler>().HandleAsync(fixture.Context(command), CancellationToken.None);
        var saved = await fixture.RequestAsync(requestId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.True(reopened.IsSuccess, reopened.Error?.Message);
        Assert.True(saved.IsOpen);
        Assert.Equal(2, saved.Generation);
        Assert.Single((await fixture.RequestHistoryAsync(requestId)).ResolutionHistory);
        Assert.Single((await fixture.RequestHistoryAsync(requestId)).Comments);
        Assert.Single((await fixture.RequestHistoryAsync(requestId)).Responses);
        Assert.Null(saved.Resolved);
        Assert.Equal("accepted", (await fixture.ResponseAsync(requestId, responseId)).Disposition!.Outcome);
    }

    [Fact]
    public async Task ShouldExplainOverdueStateFromCurrentRequestAndDateGivenReadAsOf()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var terms = await fixture.TermsAsync(requestId);
        var reader = new AuditorRequestWorkStatusReader(fixture.Requests);

        // Act
        var overdue = await reader.ReadAsync(fixture.TenantId, fixture.ProgramId, requestId, terms.DueOn.AddDays(1));
        var due = await reader.ReadAsync(fixture.TenantId, fixture.ProgramId, requestId, terms.DueOn);

        // Assert
        Assert.True(overdue.IsSuccess, overdue.Error?.Message);
        Assert.True(due.IsSuccess, due.Error?.Message);
        Assert.Equal("overdue", overdue.Value);
        Assert.Equal("open", due.Value);
    }
}
