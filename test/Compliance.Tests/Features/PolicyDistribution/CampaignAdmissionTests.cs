using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution;

public sealed class CampaignAdmissionTests
{
    [Fact]
    public async Task ShouldAdmitAcknowledgementAndReturnFixedAcceptanceGivenPersonalHttp()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var request = host.Acknowledge(campaign, host.LearnerPerson, 1);

        // Act
        var result = await host.SendAsync(request, host.Learner);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(request.CommandId, result.Value.OperationId);
        Assert.EndsWith(request.CommandId.ToString(), result.Value.StatusUrl, StringComparison.Ordinal);
        var owner = await host.CampaignAsync(campaign);
        var admitted = Assert.Single(owner.Admitted);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(2, admitted.Revision);
        Assert.Equal(1, admitted.Generation);
        Assert.Equal(PolicyCampaignParticipant.StreamOf(host.Tenant, campaign, host.LearnerPerson),
            admitted.Operation.AffectedOwner);
        Assert.Equal(owner.Stream, admitted.Operation.ExpectedRevisionOwner);
        Assert.Equal(1UL, admitted.Operation.OriginalExpectedRevision);
        Assert.Null((await host.ParticipantAsync(campaign, host.LearnerPerson)).Acknowledgement);
    }

    [Fact]
    public async Task ShouldReturnOriginalAcceptanceGivenExactRetryAfterLaterOwnerChange()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var request = host.Acknowledge(campaign, host.LearnerPerson, 1);
        var first = await host.SendAsync(request, host.Learner);
        var other = await host.SendAsync(host.Acknowledge(campaign, host.OfflinePerson, 2), host.Manager);
        host.Clock.Advance(TimeSpan.FromHours(3));
        var before = (await host.CampaignAsync(campaign)).CommittedStreamPosition;

        // Act
        var retry = await host.SendAsync(request, host.Learner);
        var after = await host.CampaignAsync(campaign);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(other.IsSuccess, other.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(before, after.CommittedStreamPosition);
        Assert.Equal(2, after.Admitted.Count);
        Assert.Equal(3, after.Revision);
    }

    [Fact]
    public async Task ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var request = host.Acknowledge(campaign, host.OfflinePerson, 1);
        var first = await host.SendAsync(request, host.Manager);
        var before = (await host.CampaignAsync(campaign)).CommittedStreamPosition;

        // Act
        var otherActor = await host.SendAsync(request, Uuid.CreateVersion4());
        var changedContent = await host.SendAsync(request with { ContentSha256 = new string('c', 64) }, host.Manager);
        var changedRevision = await host.SendAsync(request with { ExpectedCampaignRevision = 2 }, host.Manager);
        var changedPerson = await host.SendAsync(request with { PersonId = host.LearnerPerson }, host.Learner);
        var after = await host.CampaignAsync(campaign);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, otherActor.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedContent.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedRevision.Error?.Kind);
        Assert.False(changedPerson.IsSuccess);
        Assert.Equal(before, after.CommittedStreamPosition);
        Assert.Single(after.Admitted);
    }

    [Fact]
    public async Task ShouldRejectGivenMissingOrMalformedIdentity()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var before = (await host.CampaignAsync(campaign)).CommittedStreamPosition;

        // Act
        var noCommand = await host.SendAsync(host.Acknowledge(campaign, host.LearnerPerson, 1, Uuid.Empty),
            host.Learner);
        var noPerson = await host.SendAsync(host.Acknowledge(campaign, Uuid.Empty, 1), host.Manager);
        var negative = await host.SendAsync(host.Acknowledge(campaign, host.OfflinePerson, -1), host.Manager);
        var unknownCampaign = await host.SendAsync(host.Acknowledge(Uuid.CreateVersion4(), host.OfflinePerson, 1),
            host.Manager);

        // Assert
        Assert.False(noCommand.IsSuccess);
        Assert.False(noPerson.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, negative.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, unknownCampaign.Error?.Kind);
        Assert.Equal(before, (await host.CampaignAsync(campaign)).CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendGivenStaleExpectedCampaignRevision()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var before = (await host.CampaignAsync(campaign)).CommittedStreamPosition;

        // Act
        var early = await host.SendAsync(host.Acknowledge(campaign, host.OfflinePerson, 0), host.Manager);
        var late = await host.SendAsync(host.Acknowledge(campaign, host.OfflinePerson, 5), host.Manager);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, early.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, late.Error?.Kind);
        Assert.Equal(before, (await host.CampaignAsync(campaign)).CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendGivenChangedMaterialTextOrWrongKind()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var policy = await host.LaunchAsync();
        var training = await host.LaunchAsync(CampaignHost.TrainingSubject);
        var before = (await host.CampaignAsync(policy)).CommittedStreamPosition;

        // Act
        var version = await host.SendAsync(host.Acknowledge(policy, host.OfflinePerson, 1) with { PolicyVersion = 2 },
            host.Manager);
        var hash = await host.SendAsync(host.Acknowledge(policy, host.OfflinePerson, 1) with
        {
            ContentSha256 = new string('f', 64),
        }, host.Manager);
        var text = await host.SendAsync(host.Acknowledge(policy, host.OfflinePerson, 1) with
        {
            AcknowledgementText = "I agree.",
        }, host.Manager);
        var wrongKind = await host.SendAsync(CampaignHost.Acknowledgement(host.Tenant, host.Program, training,
            host.OfflinePerson, 1, subject: CampaignHost.TrainingSubject), host.Manager);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, version.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, hash.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, text.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, wrongKind.Error?.Kind);
        Assert.Contains("training campaign records completions", wrongKind.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before, (await host.CampaignAsync(policy)).CommittedStreamPosition);
        Assert.Empty((await host.CampaignAsync(training)).Admitted);
    }

    [Fact]
    public async Task ShouldRejectSecondResultOfSamePersonGivenAnotherCommand()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var first = await host.SendAsync(host.Acknowledge(campaign, host.OfflinePerson, 1), host.Manager);

        // Act
        var second = await host.SendAsync(host.Acknowledge(campaign, host.OfflinePerson, 2), host.Manager);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, second.Error?.Kind);
        Assert.Contains("already acknowledged", second.Error!.Message, StringComparison.Ordinal);
        Assert.Single((await host.CampaignAsync(campaign)).Admitted);
    }

    [Fact]
    public async Task ShouldAdmitTrainingCompletionAndWaiverWithAcceptedAcknowledgmentGivenManagementCaller()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync(CampaignHost.TrainingSubject);
        var completion = new RecordTrainingCompletion(Uuid.CreateVersion4(), host.Tenant, host.Program, campaign,
            host.LearnerPerson, 1, host.Today, "manual", " certificate.pdf ", 1);
        var waiver = new ApproveCampaignWaiver(Uuid.CreateVersion4(), host.Tenant, host.Program, campaign,
            host.OfflinePerson, " On leave ", host.Today.AddMonths(2), 2);

        // Act
        var completed = await host.SendAsync(completion, host.Manager);
        var excepted = await host.SendAsync(waiver, host.Manager);

        // Assert
        Assert.True(completed.IsSuccess, completed.Error?.Message);
        Assert.True(excepted.IsSuccess, excepted.Error?.Message);
        var owner = await host.CampaignAsync(campaign);
        Assert.Equal([CampaignParticipantResultKind.TrainingCompletion, CampaignParticipantResultKind.Waiver],
            owner.Admitted.Select(PolicyDistributionCampaign.KindOf));
        Assert.Equal(3, owner.Revision);
    }

    [Fact]
    public async Task ShouldReplayOnceGivenAppendThatCommittedBeforeTheCallerSawIt()
    {
        // Arrange
        await using var harness = new CampaignOwnerHarness();
        var roster = new RosterAudience(new Dictionary<Uuid, RosterMatch>
        {
            [harness.ManagerId] = new(harness.ManagerId, "Pat", "employee", null, new DateOnly(2025, 1, 1)),
        }, new HashSet<Uuid> { harness.ManagerId });
        var campaignId = Uuid.CreateVersion4();
        await CampaignSeed.LaunchAsync(harness.Provider, harness.TenantId, harness.ProgramId, campaignId,
            new CampaignSubject("policy", Uuid.CreateVersion4(), "POL", "Policy", 1, new string('a', 64)),
            Bdgrz.Compliance.Features.Policies.PolicyAudience.CoreSecurity, [], Uuid.CreateVersion4(),
            new string('b', 64), roster, new DateOnly(2027, 1, 1), null, harness.Manager, harness.ManagerId,
            CampaignOwnerHarness.Now);
        await using var scope = harness.Provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var executor = new CommitThenFailOnceExecutor(scope.ServiceProvider.GetRequiredService<IAggregateExecutor>());
        var admission = new PolicyParticipantAdmission(new NativeOperationExecutor(reader, executor, content), content,
            new CampaignSource(reader, content, scope.ServiceProvider.GetRequiredService<IDomainEventReader>()));
        var request = new AcknowledgePolicy(Uuid.CreateVersion4(), harness.TenantId, harness.ProgramId, campaignId,
            harness.ManagerId, 1, new string('a', 64), PolicyDistributionCampaign.DefaultAcknowledgementText, 1);
        var plan = new ParticipantAdmissionPlan(request.CommandId, harness.ManagerId, 1, nameof(AcknowledgePolicy),
            AdmittedChildKind.PolicyAcknowledgement, PolicyCampaignContent.AcknowledgeCommandSchema, harness.Manager,
            harness.Manager, false, CampaignOwnerHarness.Now);

        // Act
        var result = await admission.AdmitAsync(new Cntryl.Portia.RequestContext<AcknowledgePolicy>(request,
            new RequestDispatchContext(RequestActor.System)), harness.TenantId, harness.ProgramId, campaignId, plan, request,
            _ => ValueTask.FromResult<Bdgrz.Compliance.Features.Versioning.CommandFailure?>(null), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(request.CommandId, result.Value.OperationId);
        var owner = await harness.StateAsync(campaignId);
        Assert.Single(owner.Campaign.Admitted);
        Assert.Equal(2, owner.Campaign.Revision);
    }

    [Fact]
    public async Task ShouldRejectResultAdmissionGivenSealedOrClosedCampaign()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var closing = await host.SendAsync(new CloseCampaign(Uuid.CreateVersion4(), host.Tenant, host.Program, campaign,
            "Done", 1), host.Manager);

        // Act
        var sealedAdmission = await host.SendAsync(host.Acknowledge(campaign, host.OfflinePerson, 2), host.Manager);
        var reconcile = await host.SendAsync(new ReconcileCampaignAudience(Uuid.CreateVersion4(), host.Tenant,
            host.Program, campaign, Uuid.CreateVersion4(), 2), host.Manager);
        var secondClose = await host.SendAsync(new CloseCampaign(Uuid.CreateVersion4(), host.Tenant, host.Program,
            campaign, "Again", 2), host.Manager);

        // Assert
        Assert.True(closing.IsSuccess, closing.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, sealedAdmission.Error?.Kind);
        Assert.Contains("closing", sealedAdmission.Error!.Message, StringComparison.Ordinal);
        Assert.False(reconcile.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, secondClose.Error?.Kind);
        Assert.Empty((await host.CampaignAsync(campaign)).Admitted);
    }
}
