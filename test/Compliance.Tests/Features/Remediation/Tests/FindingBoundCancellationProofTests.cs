using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class FindingBoundCancellationProofTests
{
    [Theory]
    [InlineData("admission_event")]
    [InlineData("admission_version")]
    [InlineData("root")]
    [InlineData("generation")]
    [InlineData("intent")]
    [InlineData("foreign_action")]
    public async Task ShouldKeepExactPendingClosureGivenFailureProofSubstitution(string changed)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await FindingTests.RaiseWithCompletedActionAsync(fixture);
        var close = fixture.Close(finding);
        await PersonalReadinessClosureTransportTests.CloseHttpAsync(provider, fixture.ApproverUserId, close);
        var owner = await fixture.FindingStreamAsync(finding.FindingId, close.CommandId);
        var accepted = owner.PendingClosure!;
        var proof = new FindingClosureFailureProof(fixture.ProgramId, finding.FindingId, "A source refused.",
            finding.CorrectiveActions[0].ActionId, NativeFactReference.Of(owner.Stream, accepted), accepted.Intent,
            close.CommandId, accepted.Generation);
        proof = changed switch
        {
            "admission_event" => proof with { Admission = proof.Admission with { EventId = Uuid.CreateVersion4() } },
            "admission_version" => proof with { Admission = proof.Admission with { AggregateVersion = proof.Admission.AggregateVersion + 1 } },
            "root" => proof with { RootOperationId = Uuid.CreateVersion4() },
            "generation" => proof with { Generation = proof.Generation + 1 },
            "intent" => proof with { Intent = NativeFacts.Ref(ClosureIntent.Schema) },
            "foreign_action" => proof with { ActionId = Uuid.CreateVersion4() },
            _ => throw new InvalidOperationException("Unknown proof substitution."),
        };
        var reference = await fixture.Content.WriteJsonAsync(fixture.TenantId.ToString(), Uuid.CreateVersion4(),
            FindingContent.FailureSchema, proof);

        // Act
        var refused = await fixture.SendReceiverAsync(new AbortFindingClosure(Uuid.CreateVersion4(), close.CommandId,
            owner.Stream, accepted.Generation, 0, reference));
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.False(refused.Error!.IsTransient);
        Assert.Equal(owner.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal(accepted.Metadata.EventId, after.PendingClosure!.Metadata.EventId);
    }
}
