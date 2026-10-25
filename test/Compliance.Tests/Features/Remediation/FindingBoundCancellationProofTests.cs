using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.Readiness;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

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
