using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AuditorRequests;

public sealed class AuditorRequestTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ExaminationId = Uuid.CreateVersion4();
    static readonly Uuid RequestId = Uuid.CreateVersion4();
    static readonly Uuid OwnerId = Uuid.CreateVersion4();
    static readonly Uuid ResponderId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 17, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRecordExactRequestAndAllowOnlyExactCreateRetryGivenCreate()
    {
        // Arrange
        var request = NewRequest();
        var content = ValidContent();

        // Act
        var created = request.Create(content, OwnerId, "Owner", Now);
        var exactRetry = request.Create(content, OwnerId, "Owner", Now);
        var conflictingRetry = request.Create(content with { Wording = "Different request" },
            OwnerId, "Owner", Now);

        // Assert
        Assert.Null(created);
        Assert.Null(exactRetry);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(conflictingRetry).Code);
        Assert.Equal("open", request.Current!.Status);
        Assert.Equal(content.Wording, request.Current.Wording);
        Assert.Equal(1, request.Revision);
        Assert.Single(new AggregateScenario<AuditorRequest>(request).PendingEvents);
    }

    [Fact]
    public void ShouldRequireReviewBeforeDeliveryGivenRequiredReviewPolicy()
    {
        // Arrange
        var request = OpenRequest(requiresInternalReview: true);
        var responseId = Uuid.CreateVersion4();
        var evidenceId = Uuid.CreateVersion4();
        Assert.Null(request.CreateResponseDraft(request.Revision, responseId,
            "Evidence and explanation", [evidenceId], ResponderId, "Responder", Now.AddHours(1)));
        Assert.Null(request.SubmitResponse(request.Revision, responseId, 1,
            "Ready for review", ResponderId, "Responder", Now.AddHours(2)));

        // Act
        var deliveryBeforeReview = request.DeliverResponse(request.Revision, responseId, 1,
            "External auditor", "mail-001", ResponderId, "Responder", Now.AddHours(3));
        var approvalByAuthor = request.ReviewResponse(request.Revision, responseId, 1,
            "approved", "Complete", ResponderId, "Responder", Now.AddHours(3));
        var approval = request.ReviewResponse(request.Revision, responseId, 1,
            "approved", "Complete and scoped", ReviewerId, "Reviewer", Now.AddHours(4));
        var delivered = request.DeliverResponse(request.Revision, responseId, 1,
            "External auditor", "mail-001", ResponderId, "Responder", Now.AddHours(5));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(deliveryBeforeReview).Code);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(approvalByAuthor).Code);
        Assert.Null(approval);
        Assert.Null(delivered);
        var response = Assert.Single(request.Current!.Responses);
        Assert.Equal("delivered", response.Status);
        Assert.Equal(1, response.DeliveredVersion);
        Assert.Equal([evidenceId], response.Versions[0].EvidenceArtifactIds);
        Assert.Contains(new AggregateScenario<AuditorRequest>(request).PendingEvents,
            item => item is AuditorResponseDelivered);
    }

    [Fact]
    public void ShouldPreserveRejectedVersionAndDeliverRevisedVersionGivenResponseRevision()
    {
        // Arrange
        var request = OpenRequest(requiresInternalReview: true);
        var responseId = Uuid.CreateVersion4();
        Assert.Null(request.CreateResponseDraft(request.Revision, responseId, "First draft", [],
            ResponderId, "Responder", Now.AddHours(1)));
        Assert.Null(request.SubmitResponse(request.Revision, responseId, 1, "Submit v1",
            ResponderId, "Responder", Now.AddHours(2)));
        Assert.Null(request.ReviewResponse(request.Revision, responseId, 1, "rejected",
            "Missing support", ReviewerId, "Reviewer", Now.AddHours(3)));

        // Act
        var revision = request.ReviseResponseDraft(request.Revision, responseId, 1,
            "Revised with explanation", [Uuid.CreateVersion4()], ResponderId, "Responder",
            Now.AddHours(4));
        var submitVersionTwo = request.SubmitResponse(request.Revision, responseId, 2,
            "Submit v2", ResponderId, "Responder", Now.AddHours(5));
        var approveVersionTwo = request.ReviewResponse(request.Revision, responseId, 2,
            "approved", "Sufficient", ReviewerId, "Reviewer", Now.AddHours(6));
        var deliverVersionTwo = request.DeliverResponse(request.Revision, responseId, 2,
            "External auditor", "mail-002", ResponderId, "Responder", Now.AddHours(7));

        // Assert
        Assert.Null(revision);
        Assert.Null(submitVersionTwo);
        Assert.Null(approveVersionTwo);
        Assert.Null(deliverVersionTwo);
        var response = Assert.Single(request.Current!.Responses);
        Assert.Equal(2, response.CurrentVersion);
        Assert.Equal(2, response.DeliveredVersion);
        Assert.Equal("rejected", response.Versions[0].Review!.Outcome);
        Assert.Equal("delivered", response.Versions[1].Status);
        Assert.Equal(2, response.Versions.Count);
    }

    [Fact]
    public void ShouldBindSampleSelectionToFrozenPopulationAndKeepItImmutableGivenSelection()
    {
        // Arrange
        var request = OpenRequest(requiresInternalReview: false);
        var selectionId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var rowIds = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var manifestDigest = new string('a', 64);

        // Act
        var created = request.RecordSampleSelection(request.Revision, selectionId, populationId,
            manifestDigest, "random", rowIds, OwnerId, "Owner", Now.AddHours(1));
        var exactRetry = request.RecordSampleSelection(request.Revision, selectionId, populationId,
            manifestDigest, "random", rowIds, OwnerId, "Owner", Now.AddHours(1));
        var changedRetry = request.RecordSampleSelection(request.Revision, selectionId, populationId,
            new string('b', 64), "random", rowIds, OwnerId, "Owner", Now.AddHours(1));
        var invalid = request.RecordSampleSelection(request.Revision, Uuid.CreateVersion4(), populationId,
            "not-a-digest", "random", rowIds, OwnerId, "Owner", Now.AddHours(2));

        // Assert
        Assert.Null(created);
        Assert.Null(exactRetry);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(changedRetry).Code);
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(invalid).Code);
        var selection = Assert.Single(request.Current!.SampleSelections);
        Assert.Equal(populationId, selection.PopulationSnapshotId);
        Assert.Equal(manifestDigest, selection.PopulationContentSha256);
        Assert.Equal(rowIds.OrderBy(static id => id.ToString()), selection.SelectedRowIds);
    }

    [Fact]
    public void ShouldRetainRequestHistoryAcrossResolutionAndReopenGivenLifecycle()
    {
        // Arrange
        var request = OpenRequest(requiresInternalReview: false);
        Assert.Null(request.RecordComment(request.Revision, "Need clarification", "internal",
            OwnerId, "Owner", Now.AddHours(1)));
        var responseId = Uuid.CreateVersion4();
        Assert.Null(request.CreateResponseDraft(request.Revision, responseId, "Answer", [],
            ResponderId, "Responder", Now.AddHours(2)));
        Assert.Null(request.SubmitResponse(request.Revision, responseId, 1, "Submitted",
            ResponderId, "Responder", Now.AddHours(3)));
        Assert.Null(request.DeliverResponse(request.Revision, responseId, 1,
            "External auditor", "mail-003", ResponderId, "Responder", Now.AddHours(4)));
        Assert.Null(request.RecordResponseDisposition(request.Revision, responseId, 1,
            "accepted", "receipt-003", OwnerId, "Owner", Now.AddHours(5)));

        // Act
        var resolved = request.Resolve(request.Revision, "Auditor accepted the response",
            OwnerId, "Owner", Now.AddHours(6));
        var staleReopen = request.Reopen(request.Revision - 1, "Auditor asked a follow-up",
            OwnerId, "Owner", Now.AddHours(7));
        var reopened = request.Reopen(request.Revision, "Auditor asked a follow-up",
            OwnerId, "Owner", Now.AddHours(8));

        // Assert
        Assert.Null(resolved);
        Assert.Equal(CommandFailureCode.VersionConflict,
            Assert.IsType<CommandFailure>(staleReopen).Code);
        Assert.Null(reopened);
        Assert.Equal("open", request.Current!.Status);
        Assert.Single(request.Current.Comments);
        Assert.Single(request.Current.Responses);
        Assert.Equal("accepted", request.Current.Responses[0].Disposition!.Outcome);
        Assert.Equal(8, request.Current.History.Count);
        var events = new AggregateScenario<AuditorRequest>(request).PendingEvents.ToArray();
        var replayed = new AggregateScenario<AuditorRequest>(NewRequest()).Given(events).Aggregate;
        Assert.Equal(request.Current.Status, replayed.Current!.Status);
        Assert.Equal(request.Current.Revision, replayed.Revision);
        Assert.Equal(request.Current.History.Count, replayed.Current.History.Count);
        Assert.Equal(request.Current.Responses[0].Disposition,
            replayed.Current.Responses[0].Disposition);
    }

    [Fact]
    public void ShouldExplainOverdueStateFromCurrentRequestAndDateGivenReadAsOf()
    {
        // Arrange
        var request = OpenRequest(requiresInternalReview: false);

        // Act
        var overdue = request.GetWorkStatus(new DateOnly(2026, 10, 18));
        var due = request.GetWorkStatus(new DateOnly(2026, 10, 17));

        // Assert
        Assert.Equal("overdue", overdue);
        Assert.Equal("open", due);
    }

    static AuditorRequest NewRequest() => new(TenantId, ProgramId, RequestId);

    static AuditorRequest OpenRequest(bool requiresInternalReview)
    {
        var request = NewRequest();
        Assert.Null(request.Create(ValidContent() with
        {
            RequiresInternalReview = requiresInternalReview
        }, OwnerId, "Owner", Now));
        return request;
    }

    static AuditorRequestContent ValidContent() => new(ExaminationId, "external_email",
        "firm-ticket-12", "Provide access review evidence", "production workforce access",
        "high", OwnerId, new DateOnly(2026, 10, 17), true);
}
