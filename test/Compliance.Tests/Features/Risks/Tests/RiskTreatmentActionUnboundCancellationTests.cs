using System.Text;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Risks.Processes;
using Bdgrz.Compliance.Features.Risks.Services;
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
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskTreatmentActionUnboundCancellationTests
{
    [Fact]
    public async Task ShouldKeepLaterReviewAndEvidenceHeldGivenUnboundEarlierFailureProof()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var bus = services.GetRequiredService<IRequestBus>();
        var worker = await fixture.SeedMemberAsync();
        var evidence = await fixture.SeedEvidenceAsync(true, worker);
        var added = await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, [evidence], 0)).ExpectSuccess();
        var first = await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            fixture.Submit(added.Value.ActionId, 1, [evidence]));
        await fixture.AssignReviewAsync(first.Value.SubmissionId);
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.ApproverUserId,
            fixture.ReviewAction(added.Value.ActionId, 2, RiskStates.Reject));
        var bytes = Encoding.UTF8.GetBytes($$"""
            {"program_id":"{{fixture.ProgramId}}","risk_id":"{{fixture.RiskId}}","action_id":"{{added.Value.ActionId}}","reason":"An earlier source failed.","evidence_request_id":"{{evidence}}"}
            """);
        var failure = await services.GetRequiredService<IImmutableContentStore>().WriteVerifiedAsync(
            fixture.TenantId.ToString(), Uuid.CreateVersion4(), RiskContent.FailureSchema,
            ImmutableContentStoreExtensions.JsonMediaType, bytes, CancellationToken.None);
        var second = await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            fixture.Submit(added.Value.ActionId, 4, [evidence]));
        await fixture.AssignReviewAsync(second.Value.SubmissionId);
        var request = fixture.ReviewAction(added.Value.ActionId, 5, RiskStates.Accept);
        var admission = await bus.DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(fixture.ApproverUserId),
                new HttpInvocation("PUT", "/synthetic/review", "/synthetic/review", "synthetic")), CancellationToken.None);
        Assert.True(admission.IsSuccess, admission.Error?.Message);
        var action = await fixture.ActionAsync(request.ActionId);
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
