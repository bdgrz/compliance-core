using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.Readiness;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

public sealed class FindingCancellationProofBindingTests
{
    [Fact]
    public async Task ShouldPreserveLaterPendingClosureAndHeldSourcesGivenEarlierUnboundFailureProof()
    {
        // Arrange: an unfinished action causes the first real closure to cancel and settle.
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        finding = await fixture.AddActionAsync(finding, fixture.OwnerMemberId, fixture.Today.AddDays(7));
        var actionId = Assert.Single(finding.CorrectiveActions).ActionId;
        var first = fixture.Close(finding);
        await PersonalReadinessClosureTransportTests.CloseHttpAsync(provider, fixture.ApproverUserId, first);
        Assert.Equal(ProcessState.Completed, (await fixture.RunClosureAsync(finding.FindingId, first.CommandId)).State);
        finding = await fixture.GetFindingAsync(finding.FindingId);
        finding = await fixture.CompleteActionAsync(fixture.OwnerUserId, finding, actionId);
        var later = fixture.Close(finding);
        await PersonalReadinessClosureTransportTests.CloseHttpAsync(provider, fixture.ApproverUserId, later);
        var process = await fixture.StartClosureAsync(finding.FindingId, later.CommandId);
        var acquire = await fixture.ClosureStepAsync(process, "acquire", nameof(AuthorizeCorrectiveActionFindingClosureUse),
            Assert.Single(process.Targets));
        Assert.IsType<ReceiverOutcome.Applied>((await fixture.SendReceiverAsync(acquire)).Value);
        var held = await fixture.ActionAsync(finding.FindingId, actionId);
        Assert.True(held.Uses.HasActiveUse);
        // This is the actual old four-field proof shape. It has no binding to the earlier root or admission.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            program_id = fixture.ProgramId.ToString(),
            finding_id = finding.FindingId.ToString(),
            reason = "The earlier action was not completed.",
            action_id = actionId.ToString(),
        });
        var oldProof = await fixture.Content.WriteVerifiedAsync(fixture.TenantId.ToString(), Uuid.CreateVersion4(),
            FindingContent.FailureSchema, "application/json", bytes);
        var stream = await fixture.FindingStreamAsync(finding.FindingId);
        var abort = new AbortFindingClosure(Uuid.CreateVersion4(), later.CommandId, stream.Stream,
            stream.Generation, 0, oldProof);

        // Act
        var refused = await fixture.SendReceiverAsync(abort);
        var after = await fixture.FindingStreamAsync(finding.FindingId);
        var stillHeld = await fixture.ActionAsync(finding.FindingId, actionId);

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error!.Kind);
        Assert.False(refused.Error.IsTransient);
        Assert.Equal(later.CommandId, after.PendingClosure!.Operation.RootOperationId);
        Assert.Equal(stream.Version, after.Version);
        Assert.True(stillHeld.Uses.HasActiveUse);
        Assert.Equal(held.Version, stillHeld.Version);
    }
}
