using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Handlers;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class FindingClosureReviewHandlerTests
{
    [Fact]
    public async Task ShouldCloseReviewedProposalGivenNoPlannedActionsAndIndependentLead()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var proposed = await ProposedAsync(fixture, scope.ServiceProvider, fixture.OwnerUserId);
        var request = Review(fixture, proposed, true);
        var handler = ActivatorUtilities.CreateInstance<ReviewFindingClosureHandler>(scope.ServiceProvider);

        // Act
        var accepted = await handler.HandleAsync(ProposeFindingClosureHandlerTests.Context(request, fixture.ApproverUserId), CancellationToken.None);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        await fixture.RunClosureAsync(proposed.FindingId, request.CommandId);
        var owner = await fixture.FindingStreamAsync(proposed.FindingId);

        // Assert
        Assert.NotNull(owner.Closed);
        Assert.Equal(proposed.ClosureProposal!.ClosureId, owner.Closed.Review!.ClosureId);
        Assert.Equal(proposed.ClosureProposal.ProposalRevision, owner.Closed.Review.ProposalRevision);
        Assert.Equal(fixture.ApproverMemberId, owner.Closed.Review.ReviewerMemberId);
        Assert.Null(owner.Closed.Review.Waiver);
        Assert.Null(owner.ClosureProposal);
        Assert.Null(owner.PendingClosure);
    }

    [Fact]
    public async Task ShouldReturnCurrentProposalWithCommentsGivenIndependentLeadReview()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var proposed = await ProposedAsync(fixture, scope.ServiceProvider, fixture.OwnerUserId);
        var request = Review(fixture, proposed, false);
        var handler = ActivatorUtilities.CreateInstance<ReviewFindingClosureHandler>(scope.ServiceProvider);

        // Act
        var result = await handler.HandleAsync(ProposeFindingClosureHandlerTests.Context(request, fixture.ApproverUserId), CancellationToken.None);
        var owner = await fixture.FindingStreamAsync(proposed.FindingId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var returned = Assert.IsType<FindingClosureReturned>(owner.Latest);
        Assert.Equal(request.Comments!.Trim(), returned.Comments);
        Assert.Equal(proposed.ClosureProposal!.ClosureId, returned.Review.ClosureId);
        Assert.Equal(fixture.ApproverMemberId, returned.Review.ReviewerMemberId);
        Assert.Null(owner.ClosureProposal);
        Assert.Null(owner.PendingClosure);
        Assert.Null(owner.Closed);
        Assert.Equal("open", (await fixture.GetFindingAsync(proposed.FindingId)).Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRefuseWithoutFactGivenLeadAuthoredProposal(bool approve)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var proposed = await ProposedAsync(fixture, scope.ServiceProvider, fixture.LeadUserId);
        var before = await fixture.FindingStreamAsync(proposed.FindingId);
        var handler = ActivatorUtilities.CreateInstance<ReviewFindingClosureHandler>(scope.ServiceProvider);

        // Act
        var result = await handler.HandleAsync(ProposeFindingClosureHandlerTests.Context(Review(fixture, proposed, approve), fixture.LeadUserId), CancellationToken.None);
        var after = await fixture.FindingStreamAsync(proposed.FindingId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(before.Version, after.Version);
        Assert.Null(after.PendingClosure);
    }

    [Fact]
    public async Task ShouldRecordExactWaiverGivenLeadReviewsOwnCurrentProposal()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var proposed = await ProposedAsync(fixture, scope.ServiceProvider, fixture.LeadUserId);
        var waiver = await fixture.ApprovedWaiverAsync(WaiverScope(proposed), fixture.LeadMemberId);
        var request = Review(fixture, proposed, true) with { SeparationOfDutiesWaiverId = waiver };
        var handler = ActivatorUtilities.CreateInstance<ReviewFindingClosureHandler>(scope.ServiceProvider);

        // Act
        var accepted = await handler.HandleAsync(ProposeFindingClosureHandlerTests.Context(request, fixture.LeadUserId), CancellationToken.None);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        await fixture.RunClosureAsync(proposed.FindingId, request.CommandId);
        var owner = await fixture.FindingStreamAsync(proposed.FindingId);

        // Assert
        var binding = owner.Closed!.Review!;
        Assert.Equal(waiver, binding.Waiver!.WaiverId);
        Assert.Equal(proposed.ClosureProposal!.ClosureId, binding.Waiver.TargetVersionId);
        Assert.Equal(proposed.ClosureProposal.ProposalRevision, binding.Waiver.TargetRevision);
        Assert.Equal(fixture.LeadMemberId, binding.Waiver.ActorId);
        Assert.Equal(fixture.LeadMemberId, binding.Waiver.SubjectId);
    }

    [Fact]
    public async Task ShouldRefuseWithoutFactGivenWaiverNamesAnotherProposalRevision()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var proposed = await ProposedAsync(fixture, scope.ServiceProvider, fixture.LeadUserId);
        var waiver = await fixture.ApprovedWaiverAsync(WaiverScope(proposed) with { Revision = 2 }, fixture.LeadMemberId);
        var before = await fixture.FindingStreamAsync(proposed.FindingId);
        var handler = ActivatorUtilities.CreateInstance<ReviewFindingClosureHandler>(scope.ServiceProvider);

        // Act
        var result = await handler.HandleAsync(ProposeFindingClosureHandlerTests.Context(Review(fixture, proposed, true) with
        { SeparationOfDutiesWaiverId = waiver }, fixture.LeadUserId), CancellationToken.None);
        var after = await fixture.FindingStreamAsync(proposed.FindingId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(before.Version, after.Version);
        Assert.Null(after.PendingClosure);
    }

    [Fact]
    public async Task ShouldRefuseWithoutFactGivenWaiverHolderHasNoLeadReviewPermission()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var proposed = await ProposedAsync(fixture, scope.ServiceProvider, fixture.OwnerUserId);
        var waiver = await fixture.ApprovedWaiverAsync(WaiverScope(proposed), fixture.OwnerMemberId);
        var before = await fixture.FindingStreamAsync(proposed.FindingId);
        var handler = ActivatorUtilities.CreateInstance<ReviewFindingClosureHandler>(scope.ServiceProvider);

        // Act
        var result = await handler.HandleAsync(ProposeFindingClosureHandlerTests.Context(Review(fixture, proposed, true) with
        { SeparationOfDutiesWaiverId = waiver }, fixture.OwnerUserId), CancellationToken.None);
        var after = await fixture.FindingStreamAsync(proposed.FindingId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(before.Version, after.Version);
    }

    [Theory]
    [InlineData("mcp")]
    [InlineData("direct")]
    [InlineData("system")]
    public async Task ShouldRefusePersonalReviewGivenNonHttpOrSystemInvocation(string transport)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var proposed = await ProposedAsync(fixture, scope.ServiceProvider, fixture.OwnerUserId);
        var request = Review(fixture, proposed, true);
        var actor = transport == "system" ? RequestActor.System : ProgramManagementServices.Actor(fixture.ApproverUserId);
        RequestInvocation invocation = transport switch
        {
            "mcp" => new McpInvocation("review-finding-closure"),
            "direct" => new DirectInvocation(),
            _ => new HttpInvocation("POST", "/findings/closure/review", "/findings/closure/review", "review"),
        };
        var context = new Cntryl.Portia.RequestContext<ReviewFindingClosure>(request, new RequestDispatchContext(actor, invocation,
            new RequestMetadata(request.CommandId, Uuid.CreateVersion4(), null)));
        var before = await fixture.FindingStreamAsync(proposed.FindingId);
        var handler = ActivatorUtilities.CreateInstance<ReviewFindingClosureHandler>(scope.ServiceProvider);

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);
        var after = await fixture.FindingStreamAsync(proposed.FindingId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(before.Version, after.Version);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRefuseWithoutFactGivenReviewDoesNotNameCurrentProposal(bool wrongId)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var proposed = await ProposedAsync(fixture, scope.ServiceProvider, fixture.OwnerUserId);
        var request = Review(fixture, proposed, true);
        request = wrongId ? request with { ClosureId = Uuid.CreateVersion4() } : request with { ProposalRevision = 2 };
        var before = await fixture.FindingStreamAsync(proposed.FindingId);
        var handler = ActivatorUtilities.CreateInstance<ReviewFindingClosureHandler>(scope.ServiceProvider);

        // Act
        var result = await handler.HandleAsync(ProposeFindingClosureHandlerTests.Context(request, fixture.ApproverUserId), CancellationToken.None);
        var after = await fixture.FindingStreamAsync(proposed.FindingId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before.Version, after.Version);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldAllowIndependentProposalReviewGivenReviewerOwnsFindingOrAuthoredItsAnalysis(bool findingOwner)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var finding = await fixture.RaiseFindingAsync(findingOwner ? fixture.ApproverMemberId : fixture.OwnerMemberId);
        var artifact = await ProposeFindingClosureHandlerTests.ArtifactAsync(fixture, EvidenceInspectionOutcome.Clean);
        var proposer = ActivatorUtilities.CreateInstance<ProposeFindingClosureHandler>(scope.ServiceProvider);
        var proposed = await proposer.HandleAsync(ProposeFindingClosureHandlerTests.Context(
            ProposeFindingClosureHandlerTests.Command(fixture, finding, artifact), findingOwner ? fixture.LeadUserId : fixture.OwnerUserId), CancellationToken.None);
        Assert.True(proposed.IsSuccess, proposed.Error?.Message);
        var request = Review(fixture, proposed.Value, true);
        var reviewer = findingOwner ? fixture.ApproverUserId : fixture.LeadUserId;
        var handler = ActivatorUtilities.CreateInstance<ReviewFindingClosureHandler>(scope.ServiceProvider);

        // Act
        var accepted = await handler.HandleAsync(ProposeFindingClosureHandlerTests.Context(request, reviewer), CancellationToken.None);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        await fixture.RunClosureAsync(finding.FindingId, request.CommandId);

        // Assert
        var owner = await fixture.FindingStreamAsync(finding.FindingId);
        Assert.NotNull(owner.Closed);
        Assert.Equal(fixture.Member(reviewer), owner.Closed.Review!.ReviewerMemberId);
        Assert.Null(owner.Closed.Review.Waiver);
    }

    [Fact]
    public async Task ShouldCloseAndReleaseHeldSourceGivenReviewerResolvedActionButAuthoredNoProposalRevision()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        finding = await fixture.AddActionAsync(finding, fixture.ApproverMemberId, fixture.Today.AddDays(4));
        var actionId = Assert.Single(finding.CorrectiveActions).ActionId;
        finding = await fixture.AsAsync(fixture.ApproverUserId, fixture.Complete(finding, actionId, "Access removed.", OperationsFixture.FullSupport));
        var action = await fixture.ActionAsync(finding.FindingId, actionId);
        DomainEventRecord? source = null;
        await foreach (var fact in scope.ServiceProvider.GetRequiredService<IDomainEventReader>().ReadAsync(action.Stream, 0))
            if (fact.Event is CorrectiveActionCompletedV2)
                source = fact;
        Assert.NotNull(source);
        var report = FindingCorrectiveActionOutcomeWorkflowTests.Report(fixture, finding, source, "completed");
        var reported = await ActivatorUtilities.CreateInstance<RecordCorrectiveActionOutcomeHandler>(scope.ServiceProvider)
            .HandleAsync(new Cntryl.Portia.RequestContext<RecordCorrectiveActionOutcome>(report,
                new RequestDispatchContext(RequestActor.System)), CancellationToken.None);
        Assert.True(reported.IsSuccess, reported.Error?.Message);
        finding = await fixture.GetFindingAsync(finding.FindingId);
        var artifact = await ProposeFindingClosureHandlerTests.ArtifactAsync(fixture, EvidenceInspectionOutcome.Clean);
        var proposed = await ActivatorUtilities.CreateInstance<ProposeFindingClosureHandler>(scope.ServiceProvider).HandleAsync(
            ProposeFindingClosureHandlerTests.Context(ProposeFindingClosureHandlerTests.Command(fixture, finding, artifact), fixture.OwnerUserId), CancellationToken.None);
        Assert.True(proposed.IsSuccess, proposed.Error?.Message);
        var request = Review(fixture, proposed.Value, true);
        var handler = ActivatorUtilities.CreateInstance<ReviewFindingClosureHandler>(scope.ServiceProvider);

        // Act
        var accepted = await handler.HandleAsync(ProposeFindingClosureHandlerTests.Context(request, fixture.ApproverUserId), CancellationToken.None);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        await fixture.RunClosureAsync(finding.FindingId, request.CommandId);
        var owner = await fixture.FindingStreamAsync(finding.FindingId);
        var settled = await fixture.ActionAsync(finding.FindingId, actionId);

        // Assert
        Assert.NotNull(owner.Closed);
        Assert.Equal(fixture.ApproverMemberId, owner.Closed.Review!.ReviewerMemberId);
        Assert.Equal(fixture.ApproverMemberId, settled.Completed!.CompletedByMemberId);
        Assert.Null(owner.Closed.Review.Waiver);
        Assert.False(settled.Uses.HasActiveUse);
    }

    internal static ReviewFindingClosure Review(OperationsFixture fixture, FindingView proposed, bool approve) => new(
        Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, proposed.FindingId, proposed.Revision,
        proposed.ClosureProposal!.ClosureId, proposed.ClosureProposal.ProposalRevision, approve,
        approve ? null : " Please strengthen verification. ");

    internal static async Task<FindingView> ProposedAsync(OperationsFixture fixture, IServiceProvider services, Uuid proposer)
    {
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var artifact = await ProposeFindingClosureHandlerTests.ArtifactAsync(fixture, EvidenceInspectionOutcome.Clean);
        var handler = ActivatorUtilities.CreateInstance<ProposeFindingClosureHandler>(services);
        var result = await handler.HandleAsync(ProposeFindingClosureHandlerTests.Context(
            ProposeFindingClosureHandlerTests.Command(fixture, finding, artifact), proposer), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }

    static SeparationOfDutiesWaiverScope WaiverScope(FindingView proposed) => new(SeparationOfDutiesRecordTypes.Finding,
        proposed.FindingId, proposed.ClosureProposal!.ClosureId, proposed.ClosureProposal.ProposalRevision, SeparationOfDutiesActions.Approve);
}
