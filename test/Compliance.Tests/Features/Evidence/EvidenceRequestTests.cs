using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Evidence;

public sealed class EvidenceRequestTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid OwnerMemberId = Uuid.CreateVersion4();
    static readonly ActorReference Lead = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
    static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Due = new(2026, 10, 15);
    static readonly ImmutableContentRef Content = new(Uuid.CreateVersion4(), new string('a', 64), 1,
        "application/json", "evidence-request-test.v1");

    [Fact]
    public void ShouldOpenAnAttributedRequestGivenValidDetails()
    {
        // Arrange
        var request = New();

        // Act
        var result = Open(request);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var view = Assert.IsType<EvidenceRequestView>(request.View());
        Assert.Equal(EvidenceRequest.Open, view.Status);
        Assert.Equal(1, view.Revision);
        Assert.Equal(OwnerMemberId, view.OwnerMemberId);
        Assert.Equal(Due, view.DueOn);
        Assert.Equal(Lead, view.RequestedBy);
        Assert.IsType<EvidenceRequestOpened>(Assert.Single(new AggregateScenario<EvidenceRequest>(request).PendingEvents));
    }

    [Theory]
    [InlineData(" ", "Upload the export", 0)]
    [InlineData("Access export", " ", 0)]
    [InlineData("Access export", "Upload the export", -1)]
    public void ShouldRejectBeforeAppendGivenInvalidDetails(string title, string instructions, int dueOffsetDays)
    {
        // Arrange
        var request = New();

        // Act
        var failure = EvidenceRequestRules.ValidateOpen(title, instructions, OwnerMemberId, null,
            DateOnly.FromDateTime(Now.UtcDateTime).AddDays(dueOffsetDays), Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Empty(new AggregateScenario<EvidenceRequest>(request).PendingEvents);
    }

    [Fact]
    public void ShouldReplayIdenticalOpenGivenSameRequestId()
    {
        // Arrange
        var request = New();
        var operation = Operation(request, request.RequestId, 0);
        Assert.True(Open(request).IsSuccess);

        // Act
        var replay = request.Acceptance.Decide(AcceptedOperationBinding.Of(operation));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, replay);
        Assert.Single(new AggregateScenario<EvidenceRequest>(request).PendingEvents);
    }

    [Fact]
    public void ShouldFulfilWithAnArtifactGivenTheCurrentRevision()
    {
        // Arrange
        var request = New();
        Assert.True(Open(request).IsSuccess);
        var artifactId = Uuid.CreateVersion4();

        // Act
        var result = request.Fulfil(Operation(request, Uuid.CreateVersion4(), 1), artifactId, Lead,
            Now.AddHours(1), Content, Content);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var view = Assert.IsType<EvidenceRequestView>(request.View());
        Assert.Equal(EvidenceRequest.Fulfilled, view.Status);
        Assert.Equal(artifactId, view.ArtifactId);
        Assert.Equal(2, view.Revision);
    }

    [Fact]
    public void ShouldRejectStaleRevisionGivenConcurrentChange()
    {
        // Arrange
        var request = New();
        Assert.True(Open(request).IsSuccess);

        // Act
        var result = request.Fulfil(Operation(request, Uuid.CreateVersion4(), 2), Uuid.CreateVersion4(), Lead,
            Now, Content, Content);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, request.Revision);
    }

    [Fact]
    public void ShouldRefuseFulfilmentGivenCancelledRequest()
    {
        // Arrange
        var request = New();
        Assert.True(Open(request).IsSuccess);
        Assert.True(request.Cancel(Operation(request, Uuid.CreateVersion4(), 1), "No longer needed", Lead,
            Now, Content, Content).IsSuccess);

        // Act
        var result = request.Fulfil(Operation(request, Uuid.CreateVersion4(), 2), Uuid.CreateVersion4(), Lead,
            Now, Content, Content);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(EvidenceRequest.Cancelled, request.Status);
    }

    [Fact]
    public void ShouldRequireRationaleGivenCancellation()
    {
        // Arrange
        var request = New();
        Assert.True(Open(request).IsSuccess);

        // Act
        var failure = EvidenceRequestRules.ValidateCancel(" ");

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(EvidenceRequest.Open, request.Status);
    }

    [Fact]
    public void ShouldReportMissingRecordGivenUnknownRequest()
    {
        // Arrange
        var request = New();

        // Act
        var result = request.Fulfil(Operation(request, Uuid.CreateVersion4(), 1), Uuid.CreateVersion4(), Lead,
            Now, Content, Content);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<EvidenceRequest>(request).PendingEvents);
    }

    static EvidenceRequest New()
    {
        var id = Uuid.CreateVersion4();
        return new EvidenceRequest(TenantId, ProgramId, id, id);
    }

    static Result Open(EvidenceRequest request) => request.OpenRequest(Operation(request, request.RequestId, 0),
        new EvidenceRequestTerms("Q3 access review export", "Upload the Okta admin export.", OwnerMemberId, Due,
            null, Lead, Now), Content, Content);

    static OperationProvenance Operation(EvidenceRequest owner, Uuid commandId, ulong expected) => new(commandId,
        commandId, null, expected == 0 ? nameof(OpenEvidenceRequest) : "DecideEvidenceRequest", Lead, owner.Stream,
        owner.RequestId.ToString(), expected, new string('a', 64), Now, owner.Stream, owner.Stream);
}
