using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.Readiness;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

public sealed class FindingHistoricalClosureTests
{
    [Fact]
    public async Task ShouldResolveOriginalClosedGenerationGivenNewerClosureIsPending()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await FindingTests.RaiseWithCompletedActionAsync(fixture);
        var first = fixture.Close(finding);
        await PersonalReadinessClosureTransportTests.CloseHttpAsync(provider, fixture.ApproverUserId, first);
        var original = await fixture.StartClosureAsync(finding.FindingId, first.CommandId);
        await fixture.RunClosureAsync(finding.FindingId, first.CommandId);
        var closed = await fixture.GetFindingAsync(finding.FindingId);
        var reopened = await fixture.AsAsync(fixture.LeadUserId, new ReopenFinding(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, finding.FindingId, closed.Revision, "A later recurrence needs verification."));
        var later = fixture.Close(reopened);
        await PersonalReadinessClosureTransportTests.CloseHttpAsync(provider, fixture.ApproverUserId, later);
        var lateAcquire = (AuthorizeCorrectiveActionFindingClosureUse)await fixture.ClosureStepAsync(original, "acquire",
            nameof(AuthorizeCorrectiveActionFindingClosureUse), Assert.Single(original.Targets));
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var owner = await reader.HydrateAsync(new FindingAggregate(fixture.TenantId, fixture.ProgramId,
            finding.FindingId, selectedClosureRoot: first.CommandId));
        var before = owner.CommittedStreamPosition;

        // Act
        var historical = await FindingHistory.ReadAsync(scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), owner);
        var refused = await fixture.SendReceiverAsync(lateAcquire with { CommandId = Uuid.CreateVersion4() });
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.Equal(original.Source, NativeFactReference.Of(owner.Stream, owner.AcceptedClosure(first.CommandId)!));
        Assert.IsType<FindingClosedV2>(owner.TerminalClosure(first.CommandId));
        Assert.Single(historical.ClosureHistory);
        Assert.NotNull(historical.AcceptedClosure(later.CommandId));
        Assert.Equal(later.CommandId, owner.PendingClosure!.Operation.RootOperationId);
        Assert.IsType<ReceiverOutcome.Superseded>(refused.Value);
        Assert.Equal(before, after.CommittedStreamPosition);
        Assert.Equal(later.CommandId, after.PendingClosure!.Operation.RootOperationId);
        Assert.Null(after.Closed);
    }
}
