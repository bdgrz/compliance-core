using System.Collections;
using System.Reflection;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution;

public sealed class PolicyCampaignNativeBoundaryTests
{
    [Fact]
    public async Task ShouldRetainOnlyCurrentAuthorityGivenManyReconciliationsAdmissionsAndCanceledClosures()
    {
        // Arrange
        await using var host = new CampaignHost();
        var campaignId = await host.LaunchAsync(CampaignHost.PolicySubject, host.OfflinePerson);
        var services = host.Scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var executor = services.GetRequiredService<IAggregateExecutor>();
        var history = services.GetRequiredService<PolicyCampaignReader>();
        var source = services.GetRequiredService<CampaignSource>();
        var close = new CloseCampaignHandler(new NativeOperationExecutor(reader, executor, host.Content), source,
            host.Content, host.Clock);
        var firstResult = Uuid.Empty;

        // Act
        for (var index = 0; index < 105; index++)
        {
            var commandId = Uuid.CreateVersion4();
            var snapshotId = Uuid.CreateVersion4();
            var current = await reader.HydrateAsync(new PolicyDistributionCampaign(host.Tenant, campaignId, commandId, snapshotId));
            var request = new ReconcileCampaignAudience(commandId, host.Tenant, host.Program, campaignId,
                snapshotId, current.Revision);
            var reconciliation = PolicyCampaignContent.Provenance(commandId, nameof(ReconcileCampaignAudience),
                current.Stream, current.Stream, host.ManagerActor, current.Revision, AuthoredIntentDigest.Compute(request),
                host.Clock.GetUtcNow());
            var amendments = await PolicyCampaignContent.WriteAsync(host.Content, host.Tenant,
                PolicyCampaignContent.AmendmentsSchema, (IReadOnlyList<CampaignAudienceAmendment>)[], CancellationToken.None);
            var body = await PolicyCampaignContent.WriteAsync(host.Content, host.Tenant,
                PolicyCampaignContent.ReconciliationSchema, new CampaignReconciliationView(campaignId,
                    current.Reconciliations + 1, snapshotId, []), CancellationToken.None);
            var result = await PolicyCampaignContent.WriteResultAsync(host.Content, host.Tenant,
                PolicyCampaignContent.ReconciliationSchema, body, CancellationToken.None);
            var first = await FirstResponses.FreezeAsync(host.Content, host.Tenant.ToString(),
                PolicyCampaignContent.OkResponse(PolicyCampaignContent.ReconciliationSchema, body));
            var reconciled = await executor.ExecuteAsync(new PolicyDistributionCampaign(host.Tenant, campaignId, commandId,
                snapshotId), owner => AggregateOutcome.Commit(owner.Reconcile(reconciliation, snapshotId, new string('d', 64),
                    host.Today, amendments, host.ManagerActor, host.Clock.GetUtcNow(), first, result)),
                new RequestDispatchContext(RequestActor.System));
            Assert.True(reconciled.IsSuccess, reconciled.Error?.Message);
            var admittedRoot = Uuid.CreateVersion4();
            if (index == 0)
                firstResult = admittedRoot;
            var applied = await CampaignSeed.ApproveWaiverAsync(host.Provider, host.Tenant, host.Program, campaignId,
                host.OfflinePerson, "Exception " + index, host.Today.AddDays(1), host.ManagerActor,
                host.Clock.GetUtcNow(), admittedRoot);
            Assert.True(applied.IsSuccess, applied.Error?.Message);
            var closureId = Uuid.CreateVersion4();
            current = await reader.HydrateAsync(new PolicyDistributionCampaign(host.Tenant, campaignId, closureId));
            var state = await source.StateOfAsync(current, CancellationToken.None);
            Assert.True(state.IsSuccess, state.Error?.Message);
            var decision = new CloseCampaign(closureId, host.Tenant, host.Program, campaignId, "Close this generation.",
                current.Revision);
            var closure = PolicyCampaignContent.Provenance(closureId, nameof(CloseCampaign), current.Stream,
                current.Stream, host.ManagerActor, current.Revision, AuthoredIntentDigest.Compute(decision), host.Clock.GetUtcNow());
            var intent = await close.FreezeAsync(state.Value, decision, closure, host.Clock.GetUtcNow(), CancellationToken.None);
            var response = await FirstResponses.FreezeAsync(host.Content, host.Tenant.ToString(),
                await PolicyCampaignContent.AcceptedResponseAsync(host.Content, host.Tenant, closureId,
                    host.Clock.GetUtcNow(), CancellationToken.None));
            var accepted = await executor.ExecuteAsync(new PolicyDistributionCampaign(host.Tenant, campaignId, closureId),
                owner => AggregateOutcome.Commit(owner.AcceptClosure(closure, intent, response)), new RequestDispatchContext(RequestActor.System));
            Assert.True(accepted.IsSuccess, accepted.Error?.Message);
            var saved = await history.ReadAsync(host.Tenant, campaignId, selectedClosureRootId: closureId);
            var pending = saved.ClosureFor(closureId)!;
            var aborted = await host.DispatchAsync(new AbortPolicyCampaignClosure(Uuid.CreateVersion4(), closureId,
                saved.Stream, pending.Generation, pending.Revision, intent));
            Assert.True(aborted.IsSuccess, aborted.Error?.Message);
        }
        var campaign = await history.ReadAsync(host.Tenant, campaignId);
        var participant = await history.ReadParticipantAsync(host.Tenant, campaignId, host.OfflinePerson);
        var retained = new Aggregate[] { campaign.Owner, participant.Owner }.SelectMany(owner => owner.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(field => field.GetValue(owner) is ICollection { Count: > 2 })).ToArray();

        // Assert
        Assert.Equal(421, campaign.Revision);
        Assert.Equal(106, campaign.Generation);
        Assert.Equal(105, campaign.Reconciliations);
        Assert.Equal(105, participant.Revision);
        Assert.NotNull(campaign.AdmittedFor(firstResult));
        Assert.NotNull(participant.ResultFor(firstResult));
        Assert.Empty(retained);
        Assert.Equal(105, campaign.Amendments.Count);
        Assert.Equal(105, campaign.Admitted.Count);
        Assert.Equal(105, participant.Waivers.Count);
        Assert.Equal("Exception 0", participant.Waivers[0].Reason);
        Assert.Equal("Exception 104", participant.Waivers[^1].Reason);
    }
}
