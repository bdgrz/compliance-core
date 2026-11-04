using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Handlers;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Reactors;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class FindingClosureWithdrawalWorkflowTests
{
    [Fact]
    public async Task ShouldWithdrawCurrentProposalGivenItsProposer()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var proposed = await FindingClosureReviewHandlerTests.ProposedAsync(fixture, scope.ServiceProvider, fixture.OwnerUserId);
        var request = Withdraw(fixture, proposed);
        var handler = ActivatorUtilities.CreateInstance<WithdrawFindingClosureHandler>(scope.ServiceProvider);

        // Act
        var result = await handler.HandleAsync(ProposeFindingClosureHandlerTests.Context(request, fixture.OwnerUserId), CancellationToken.None);
        var owner = await fixture.FindingStreamAsync(proposed.FindingId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("open", result.Value.Status);
        Assert.Null(result.Value.ClosureProposal);
        Assert.Null(owner.ClosureProposal);
        Assert.Null(owner.PendingClosure);
        Assert.Null(Assert.IsType<FindingClosureWithdrawn>(owner.Latest).CanceledReview);
        Assert.Equal(WireJson.Of(result.Value), WireJson.Of(await fixture.GetFindingAsync(proposed.FindingId)));
    }

    [Fact]
    public async Task ShouldRefuseWithoutFactGivenLeadIsNotCurrentProposer()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var proposed = await FindingClosureReviewHandlerTests.ProposedAsync(fixture, scope.ServiceProvider, fixture.OwnerUserId);
        var before = await fixture.FindingStreamAsync(proposed.FindingId);
        var handler = ActivatorUtilities.CreateInstance<WithdrawFindingClosureHandler>(scope.ServiceProvider);

        // Act
        var result = await handler.HandleAsync(ProposeFindingClosureHandlerTests.Context(Withdraw(fixture, proposed), fixture.LeadUserId), CancellationToken.None);
        var after = await fixture.FindingStreamAsync(proposed.FindingId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(before.Version, after.Version);
        Assert.NotNull(after.ClosureProposal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRefuseWithoutFactGivenWithdrawalNamesAnotherProposal(bool wrongId)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var proposed = await FindingClosureReviewHandlerTests.ProposedAsync(fixture, scope.ServiceProvider, fixture.OwnerUserId);
        var request = Withdraw(fixture, proposed);
        request = wrongId ? request with { ClosureId = Uuid.CreateVersion4() } : request with { ProposalRevision = 2 };
        var before = await fixture.FindingStreamAsync(proposed.FindingId);
        var handler = ActivatorUtilities.CreateInstance<WithdrawFindingClosureHandler>(scope.ServiceProvider);

        // Act
        var result = await handler.HandleAsync(ProposeFindingClosureHandlerTests.Context(request, fixture.OwnerUserId), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before.Version, (await fixture.FindingStreamAsync(proposed.FindingId)).Version);
    }

    [Fact]
    public async Task ShouldFenceReviewAndReleaseSourcesGivenCommittedWithdrawalDeliveredTwice()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var held = await HeldAsync(fixture, scope.ServiceProvider);
        var current = await fixture.GetFindingAsync(held.Proposed.FindingId);
        var before = await fixture.ActionAsync(current.FindingId, Assert.Single(current.CorrectiveActions).ActionId);
        Assert.True(before.Uses.HasActiveUse);
        var handler = ActivatorUtilities.CreateInstance<WithdrawFindingClosureHandler>(scope.ServiceProvider);
        var withdrawn = await handler.HandleAsync(ProposeFindingClosureHandlerTests.Context(Withdraw(fixture, current), fixture.OwnerUserId), CancellationToken.None);
        Assert.True(withdrawn.IsSuccess, withdrawn.Error?.Message);
        var source = await WithdrawalSourceAsync(fixture, current.FindingId, scope.ServiceProvider);
        var reactor = ActivatorUtilities.CreateInstance<FindingClosureWithdrawalReactor>(scope.ServiceProvider, new InMemoryProjectionCheckpointStore());
        await new ReactorScenario(new TenantId(fixture.TenantId.ToString())).RunAsync(reactor);

        // Act
        Assert.True((await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<FindingClosureWithdrawn>(source), CancellationToken.None)).IsSuccess);
        var once = await fixture.ActionAsync(current.FindingId, Assert.Single(current.CorrectiveActions).ActionId);
        Assert.True((await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<FindingClosureWithdrawn>(source), CancellationToken.None)).IsSuccess);
        var twice = await fixture.ActionAsync(current.FindingId, Assert.Single(current.CorrectiveActions).ActionId);
        var owner = await fixture.FindingStreamAsync(current.FindingId, held.Review.CommandId);

        // Assert
        Assert.False(once.Uses.HasActiveUse);
        Assert.Equal(once.Version, twice.Version);
        Assert.Null(owner.PendingClosure);
        Assert.Null(owner.Closed);
        Assert.IsType<FindingClosureWithdrawn>(owner.TerminalClosure(held.Review.CommandId));
        var binding = Assert.IsType<FindingClosureWithdrawn>(source.Event).CanceledReview!;
        Assert.Equal(held.Review.CommandId, binding.RootOperationId);
        Assert.Equal(owner.AcceptedClosure(held.Review.CommandId)!.Intent, binding.Intent);
    }

    [Fact]
    public async Task ShouldKeepLaterProposalOpenGivenWithdrawnReviewCompletionArrivesLate()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var held = await HeldAsync(fixture, scope.ServiceProvider);
        var current = await fixture.GetFindingAsync(held.Proposed.FindingId);
        var withdrawal = await ActivatorUtilities.CreateInstance<WithdrawFindingClosureHandler>(scope.ServiceProvider)
            .HandleAsync(ProposeFindingClosureHandlerTests.Context(Withdraw(fixture, current), fixture.OwnerUserId), CancellationToken.None);
        Assert.True(withdrawal.IsSuccess, withdrawal.Error?.Message);
        var version = held.Proposed.ClosureProposal!;
        var next = new ProposeFindingClosure(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, current.FindingId,
            withdrawal.Value.Revision, version.ClosureId, version.ProposalRevision, version.Terms);
        var proposed = await ActivatorUtilities.CreateInstance<ProposeFindingClosureHandler>(scope.ServiceProvider)
            .HandleAsync(ProposeFindingClosureHandlerTests.Context(next, fixture.OwnerUserId), CancellationToken.None);
        Assert.True(proposed.IsSuccess, proposed.Error?.Message);
        var before = await fixture.FindingStreamAsync(current.FindingId);

        // Act
        var delayed = await fixture.SendReceiverAsync(held.Complete);
        var after = await fixture.FindingStreamAsync(current.FindingId);
        var action = await fixture.ActionAsync(current.FindingId, Assert.Single(current.CorrectiveActions).ActionId);

        // Assert
        Assert.True(delayed.IsSuccess, delayed.Error?.Message);
        Assert.IsType<ReceiverOutcome.Superseded>(delayed.Value);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(version.ClosureId, after.ClosureProposal!.ClosureId);
        Assert.Equal(2, after.ClosureProposal.ProposalRevision);
        Assert.Null(after.Closed);
        Assert.False(action.Uses.HasActiveUse);
    }

    [Fact]
    public async Task ShouldRefuseWithoutWithdrawalFactGivenReviewAlreadyClosedFinding()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var proposed = await FindingClosureReviewHandlerTests.ProposedAsync(fixture, scope.ServiceProvider, fixture.OwnerUserId);
        var review = FindingClosureReviewHandlerTests.Review(fixture, proposed, true);
        Assert.True((await ActivatorUtilities.CreateInstance<ReviewFindingClosureHandler>(scope.ServiceProvider)
            .HandleAsync(ProposeFindingClosureHandlerTests.Context(review, fixture.ApproverUserId), CancellationToken.None)).IsSuccess);
        await fixture.RunClosureAsync(proposed.FindingId, review.CommandId);
        var before = await fixture.FindingStreamAsync(proposed.FindingId);
        var request = Withdraw(fixture, proposed) with { ExpectedRevision = before.Revision };

        // Act
        var result = await ActivatorUtilities.CreateInstance<WithdrawFindingClosureHandler>(scope.ServiceProvider)
            .HandleAsync(ProposeFindingClosureHandlerTests.Context(request, fixture.OwnerUserId), CancellationToken.None);
        var after = await fixture.FindingStreamAsync(proposed.FindingId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before.Version, after.Version);
        Assert.NotNull(after.Closed);
    }

    [Fact]
    public async Task ShouldRejectAlteredTriggerBeforeCleanupGivenCommittedWithdrawalSource()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var held = await HeldAsync(fixture, scope.ServiceProvider);
        var current = await fixture.GetFindingAsync(held.Proposed.FindingId);
        Assert.True((await ActivatorUtilities.CreateInstance<WithdrawFindingClosureHandler>(scope.ServiceProvider)
            .HandleAsync(ProposeFindingClosureHandlerTests.Context(Withdraw(fixture, current), fixture.OwnerUserId), CancellationToken.None)).IsSuccess);
        var source = await WithdrawalSourceAsync(fixture, current.FindingId, scope.ServiceProvider);
        var actual = Assert.IsType<FindingClosureWithdrawn>(source.Event);
        var changed = actual with { CanceledReview = null };
        changed.AttachMetadata(actual.Metadata);
        var altered = new DomainEventRecord(source.Stream, changed, source.ResourceOffset, source.NextCursor);
        var reactor = ActivatorUtilities.CreateInstance<FindingClosureWithdrawalReactor>(scope.ServiceProvider, new InMemoryProjectionCheckpointStore());
        await new ReactorScenario(new TenantId(fixture.TenantId.ToString())).RunAsync(reactor);
        var before = await fixture.ActionAsync(current.FindingId, Assert.Single(current.CorrectiveActions).ActionId);

        // Act
        var error = await Assert.ThrowsAsync<ReactionCommandFailedException>(async () =>
            await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<FindingClosureWithdrawn>(altered), CancellationToken.None));
        var after = await fixture.ActionAsync(current.FindingId, Assert.Single(current.CorrectiveActions).ActionId);

        // Assert
        Assert.False(error.Error.IsTransient);
        Assert.Equal(before.Version, after.Version);
        Assert.True(after.Uses.HasActiveUse);
    }

    static WithdrawFindingClosure Withdraw(OperationsFixture fixture, FindingView current) => new(Uuid.CreateVersion4(),
        fixture.TenantId, fixture.ProgramId, current.FindingId, current.Revision, current.ClosureProposal!.ClosureId,
        current.ClosureProposal.ProposalRevision);

    static async Task<Held> HeldAsync(OperationsFixture fixture, IServiceProvider services)
    {
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        finding = await fixture.AddActionAsync(finding, fixture.ApproverMemberId, fixture.Today.AddDays(4));
        var actionId = Assert.Single(finding.CorrectiveActions).ActionId;
        finding = await fixture.AsAsync(fixture.ApproverUserId, fixture.Complete(finding, actionId, "Removed access.", OperationsFixture.FullSupport));
        var action = await fixture.ActionAsync(finding.FindingId, actionId);
        DomainEventRecord? source = null;
        await foreach (var item in services.GetRequiredService<IDomainEventReader>().ReadAsync(action.Stream, 0))
            if (item.Event is CorrectiveActionCompletedV2)
                source = item;
        Assert.NotNull(source);
        var report = FindingCorrectiveActionOutcomeWorkflowTests.Report(fixture, finding, source, "completed");
        Assert.True((await ActivatorUtilities.CreateInstance<RecordCorrectiveActionOutcomeHandler>(services)
            .HandleAsync(new Cntryl.Portia.RequestContext<RecordCorrectiveActionOutcome>(report,
                new RequestDispatchContext(RequestActor.System)), CancellationToken.None)).IsSuccess);
        finding = await fixture.GetFindingAsync(finding.FindingId);
        var artifact = await ProposeFindingClosureHandlerTests.ArtifactAsync(fixture, EvidenceInspectionOutcome.Clean);
        var proposed = await ActivatorUtilities.CreateInstance<ProposeFindingClosureHandler>(services).HandleAsync(
            ProposeFindingClosureHandlerTests.Context(ProposeFindingClosureHandlerTests.Command(fixture, finding, artifact), fixture.OwnerUserId), CancellationToken.None);
        Assert.True(proposed.IsSuccess, proposed.Error?.Message);
        var review = FindingClosureReviewHandlerTests.Review(fixture, proposed.Value, true);
        Assert.True((await ActivatorUtilities.CreateInstance<ReviewFindingClosureHandler>(services)
            .HandleAsync(ProposeFindingClosureHandlerTests.Context(review, fixture.ApproverUserId), CancellationToken.None)).IsSuccess);
        var record = await fixture.StartClosureAsync(finding.FindingId, review.CommandId);
        var acquire = await fixture.ClosureStepAsync(record, FindingClosureProcess.Acquire,
            nameof(AuthorizeCorrectiveActionFindingClosureUse), Assert.Single(record.Targets));
        var acquired = await fixture.SendReceiverAsync(acquire);
        Assert.True(acquired.IsSuccess, acquired.Error?.Message);
        var complete = await fixture.ClosureStepAsync(record, FindingClosureProcess.Complete, nameof(CompleteFindingClosure), null);
        return new Held(proposed.Value, review, complete);
    }

    static async Task<DomainEventRecord> WithdrawalSourceAsync(OperationsFixture fixture, Uuid findingId, IServiceProvider services)
    {
        var owner = await fixture.FindingStreamAsync(findingId);
        await foreach (var source in services.GetRequiredService<IDomainEventReader>().ReadAsync(owner.Stream, 0))
            if (source.Event is FindingClosureWithdrawn)
                return source;
        throw new InvalidOperationException("The withdrawal was not committed.");
    }

    sealed record Held(FindingView Proposed, ReviewFindingClosure Review, IRequest<ReceiverOutcome> Complete);
}
