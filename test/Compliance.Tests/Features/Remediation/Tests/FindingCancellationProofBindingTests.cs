using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

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
