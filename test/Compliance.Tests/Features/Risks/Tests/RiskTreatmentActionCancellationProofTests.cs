using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Processes;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskTreatmentActionCancellationProofTests
{
    [Theory]
    [InlineData("earlier_admission")]
    [InlineData("earlier_root")]
    [InlineData("wrong_generation")]
    [InlineData("earlier_intent")]
    [InlineData("outside_evidence")]
    [InlineData("unbound_legacy_schema")]
    public async Task ShouldRefuseCancellationWithoutReleasingHeldEvidenceGivenAnotherReviewProof(string mismatch)
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var evidence = await fixture.SeedEvidenceAsync(true, worker);
        var added = await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, [evidence], 0)).ExpectSuccess();
        var first = await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            fixture.Submit(added.Value.ActionId, 1, [evidence]));
        await fixture.AssignReviewAsync(first.Value.SubmissionId);
        var firstReview = fixture.ReviewAction(added.Value.ActionId, 2, RiskStates.Reject);
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.ApproverUserId, firstReview);
        var second = await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            fixture.Submit(added.Value.ActionId, 4, [evidence]));
        await fixture.AssignReviewAsync(second.Value.SubmissionId);
        var request = fixture.ReviewAction(added.Value.ActionId, 5, RiskStates.Accept);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var bus = services.GetRequiredService<IRequestBus>();
        var admitted = await bus.DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(fixture.ApproverUserId),
                new HttpInvocation("PUT", "/synthetic/review", "/synthetic/review", "synthetic")), CancellationToken.None);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        var reader = services.GetRequiredService<IAggregateReader>();
        var action = await reader.HydrateAsync(new RiskTreatmentAction(fixture.TenantId, fixture.ProgramId,
            fixture.RiskId, request.ActionId, selectedReviewRoot: firstReview.CommandId));
        var oldAdmission = action.AcceptedReview(firstReview.CommandId)!;
        var accepted = action.PendingReview!;
        var source = NativeFactReference.Of(action.Stream, accepted);
        var target = RiskActionReviewEffects.EvidenceKey(fixture.TenantId, fixture.ProgramId, evidence);
        var process = ProcessRecord.Start(ProcessKey.ForRoot(RiskActionReviewEffects.ProcessName, request.CommandId,
            source), source, accepted.Operation.Episode, accepted.Intent, [target], DateTimeOffset.UtcNow);
        var acquireId = RiskActionReviewEffects.ReceiverCommandId(source.EventId,
            nameof(AuthorizeEvidenceRequestActionCompletionUse), target, accepted.Operation.Episode);
        var acquire = await RiskActionCompletionReviewProcess.Definition().CreateReceiver(new ProcessStep(process,
            "acquire", nameof(AuthorizeEvidenceRequestActionCompletionUse), target, acquireId), services, CancellationToken.None);
        var held = await bus.DispatchAsync(acquire!, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(held.IsSuccess, held.Error?.Message);
        Assert.IsType<ReceiverOutcome.Applied>(held.Value);
        var proof = new RiskActionReviewFailureProof(fixture.ProgramId, fixture.RiskId, request.ActionId,
            "The selected source can no longer authorize this review.", evidence, source, request.CommandId,
            accepted.Generation, accepted.Intent);
        proof = mismatch switch
        {
            "earlier_admission" => proof with { Admission = NativeFactReference.Of(action.Stream, oldAdmission) },
            "earlier_root" => proof with { OperationId = firstReview.CommandId },
            "wrong_generation" => proof with { Generation = accepted.Generation + 1 },
            "earlier_intent" => proof with { Intent = oldAdmission.Intent },
            "outside_evidence" => proof with { EvidenceRequestId = Uuid.CreateVersion4() },
            _ => proof,
        };
        var failure = await FindingContent.WriteAsync(services.GetRequiredService<IImmutableContentStore>(),
            fixture.TenantId, mismatch == "unbound_legacy_schema"
                ? "RiskTreatmentActionCompletionReviewCanceled.FailureProof.v1" : RiskContent.FailureSchema,
            proof, CancellationToken.None);
        var command = new AbortRiskTreatmentActionReview(Uuid.CreateVersion4(), request.CommandId, action.Stream,
            accepted.Generation, 0, failure);
        var before = action.CommittedStreamPosition;

        // Act
        var refused = await bus.DispatchAsync(command, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var after = await fixture.ActionAsync(request.ActionId);
        var retainedEvidence = await reader.HydrateAsync(new EvidenceRequest(fixture.TenantId, fixture.ProgramId, evidence));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.False(refused.Error!.IsTransient);
        Assert.Equal(before, after.CommittedStreamPosition);
        Assert.Equal(accepted.Metadata.EventId, after.PendingReview!.Metadata.EventId);
        Assert.True(retainedEvidence.Uses.HasActiveUse);
    }
}
