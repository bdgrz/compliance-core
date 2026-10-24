using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution;

public sealed class PolicyParticipantResultProcessTests
{
    [Fact]
    public async Task ShouldApplyAdmittedAcknowledgementsToTheirParticipantsGivenRootReactor()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var personal = host.Acknowledge(campaign, host.LearnerPerson, 1);
        var proxy = host.Acknowledge(campaign, host.OfflinePerson, 2);
        Assert.True((await host.SendAsync(personal, host.Learner)).IsSuccess);
        Assert.True((await host.SendAsync(proxy, host.Manager)).IsSuccess);

        // Act
        await host.ReactResultsAsync(campaign);
        await host.ReactResultsAsync(campaign);
        var first = await host.RunAsync(await host.ResultKey(personal.CommandId));
        var second = await host.RunAsync(await host.ResultKey(proxy.CommandId));

        // Assert
        Assert.Equal(ProcessState.Completed, first.State);
        Assert.Equal(ProcessState.Completed, second.State);
        Assert.Single(first.Targets);
        var self = (await host.ParticipantAsync(campaign, host.LearnerPerson)).Acknowledgement!;
        Assert.False(self.RecordedOnBehalf);
        Assert.Equal(host.LearnerPerson.ToString(), self.Performer.Id);
        Assert.Equal(RbacIds.Member(host.Tenant, host.Learner).ToString(), self.Recorder.Id);
        Assert.Equal(personal.CommandId, self.AcknowledgementId);
        Assert.Equal(personal.CommandId, self.Operation.RootOperationId);
        Assert.Null(self.FirstResponse);
        Assert.NotNull(self.OperationResult);
        var onBehalf = (await host.ParticipantAsync(campaign, host.OfflinePerson)).Acknowledgement!;
        Assert.True(onBehalf.RecordedOnBehalf);
        Assert.Equal(host.ManagerMember.ToString(), onBehalf.Recorder.Id);
        var state = await host.StateAsync(campaign);
        Assert.Equal(2, state.Totals(host.Today).Satisfied);
        Assert.Equal(first.Source.EventId, self.Operation.CausationEventId);
    }

    [Fact]
    public async Task ShouldApplyCompletionAndWaiverWithTheirTerminalViewsGivenTrainingCampaign()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync(CampaignHost.TrainingSubject);
        var completion = new RecordTrainingCompletion(Uuid.CreateVersion4(), host.Tenant, host.Program, campaign,
            host.LearnerPerson, 1, host.Today, "manual", " certificate.pdf ", 1);
        var waiver = new ApproveCampaignWaiver(Uuid.CreateVersion4(), host.Tenant, host.Program, campaign,
            host.OfflinePerson, " On leave ", host.Today.AddMonths(2), 2);
        Assert.True((await host.SendAsync(completion, host.Manager)).IsSuccess);
        Assert.True((await host.SendAsync(waiver, host.Manager)).IsSuccess);

        // Act
        await host.ReactResultsAsync(campaign);
        await host.RunAsync(await host.ResultKey(completion.CommandId));
        await host.RunAsync(await host.ResultKey(waiver.CommandId));

        // Assert
        var completed = (await host.ParticipantAsync(campaign, host.LearnerPerson)).Completion!;
        Assert.Equal("certificate.pdf", completed.EvidenceReference);
        Assert.Equal(completion.CommandId, completed.CompletionId);
        Assert.Equal(CampaignHost.TrainingSubject.RecordId, completed.RequirementId);
        var excepted = Assert.Single((await host.ParticipantAsync(campaign, host.OfflinePerson)).Waivers);
        Assert.Equal("On leave", excepted.Reason);
        Assert.Equal(host.ManagerMember, excepted.ApproverMemberId);
        var state = await host.StateAsync(campaign);
        Assert.Equal(1, state.Totals(host.Today).Satisfied);
        Assert.Equal(1, state.Totals(host.Today).Excepted);
    }

    [Fact]
    public async Task ShouldReturnOriginalFactGivenReplayAndSecondCommandForSameAdmission()
    {
        // Arrange
        var hold = new CrashAfterReplaces();
        await using var host = new CampaignHost(hold.Wrap);
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var request = host.Acknowledge(campaign, host.LearnerPerson, 1);
        Assert.True((await host.SendAsync(request, host.Learner)).IsSuccess);
        hold.ArmAfterReplaceCount(0);
        await host.ReactResultsAsync(campaign, tolerateCrash: true);
        hold.Disarm();
        var record = (await host.RecordAsync(await host.ResultKey(request.CommandId)))!;
        var command = (ApplyPolicyParticipantResult)(await host.StepAsync(record, "materialize",
            nameof(ApplyPolicyParticipantResult), record.Targets[0]))!;

        // Act
        var applied = await host.DispatchAsync(command);
        var replay = await host.DispatchAsync(command);
        var other = await host.DispatchAsync(command with { CommandId = Uuid.CreateVersion4() });
        var participant = await host.ParticipantAsync(campaign, host.LearnerPerson);

        // Assert
        var fact = Assert.IsType<ReceiverOutcome.Applied>(applied.Value).Fact;
        Assert.Equal(fact, Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
        Assert.Equal(fact, Assert.IsType<ReceiverOutcome.Replayed>(other.Value).Fact);
        Assert.Single(await host.EventsAsync<PolicyAcknowledgementRecorded>(participant.Stream));
        Assert.Equal(1, participant.Revision);
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendGivenWrongOrForgedSourceProof()
    {
        // Arrange
        var hold = new CrashAfterReplaces();
        await using var host = new CampaignHost(hold.Wrap);
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var first = host.Acknowledge(campaign, host.LearnerPerson, 1);
        var second = host.Acknowledge(campaign, host.OfflinePerson, 2);
        Assert.True((await host.SendAsync(first, host.Learner)).IsSuccess);
        Assert.True((await host.SendAsync(second, host.Manager)).IsSuccess);
        hold.ArmAfterReplaceCount(0);
        await host.ReactResultsAsync(campaign, tolerateCrash: true);
        hold.Disarm();
        var record = (await host.RecordAsync(await host.ResultKey(first.CommandId)))!;
        var otherRecord = (await host.RecordAsync(await host.ResultKey(second.CommandId)))!;
        var command = (ApplyPolicyParticipantResult)(await host.StepAsync(record, "materialize",
            nameof(ApplyPolicyParticipantResult), record.Targets[0]))!;
        var otherCommand = (ApplyPolicyParticipantResult)(await host.StepAsync(otherRecord, "materialize",
            nameof(ApplyPolicyParticipantResult), otherRecord.Targets[0]))!;

        // Act
        var forged = await host.DispatchAsync(command with { Source = command.Source with { EventId = Uuid.CreateVersion4() } });
        var swappedIntent = await host.DispatchAsync(command with { Intent = otherCommand.Intent });
        var swappedTarget = await host.DispatchAsync(command with { Target = otherCommand.Target });
        var wrongKind = await host.DispatchAsync(command with { ResultKind = CampaignParticipantResultKind.Waiver });
        var wrongPerson = await host.DispatchAsync(command with { PersonId = host.OfflinePerson });
        var wrongGeneration = await host.DispatchAsync(command with { Generation = 2 });
        var unknownOperation = await host.DispatchAsync(command with { OperationId = Uuid.CreateVersion4() });

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, forged.Error?.Kind);
        Assert.False(forged.Error!.IsTransient);
        foreach (var result in new[] { swappedIntent, swappedTarget, wrongKind, wrongPerson, wrongGeneration })
            Assert.False(result.IsSuccess, result.Error?.Message);
        Assert.True(unknownOperation.Error!.IsTransient);
        Assert.Empty(await host.EventsAsync<PolicyAcknowledgementRecorded>(
            (await host.ParticipantAsync(campaign, host.LearnerPerson)).Stream));
        Assert.Empty(await host.EventsAsync<PolicyAcknowledgementRecorded>(
            (await host.ParticipantAsync(campaign, host.OfflinePerson)).Stream));
    }

    [Fact]
    public async Task ShouldRejectReceiverGivenNonSystemCaller()
    {
        // Arrange
        var hold = new CrashAfterReplaces();
        await using var host = new CampaignHost(hold.Wrap);
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var request = host.Acknowledge(campaign, host.LearnerPerson, 1);
        Assert.True((await host.SendAsync(request, host.Learner)).IsSuccess);
        hold.ArmAfterReplaceCount(0);
        await host.ReactResultsAsync(campaign, tolerateCrash: true);
        hold.Disarm();
        var record = (await host.RecordAsync(await host.ResultKey(request.CommandId)))!;
        var command = (await host.StepAsync(record, "materialize", nameof(ApplyPolicyParticipantResult),
            record.Targets[0]))!;

        // Act
        var result = await host.SendAsync(command, host.Manager);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Null((await host.ParticipantAsync(campaign, host.LearnerPerson)).Acknowledgement);
    }

    [Fact]
    public async Task ShouldMatchTheDesignCatalogGivenBothCampaignProcessDefinitions()
    {
        // Arrange
        await using var host = new CampaignHost();

        // Act
        var results = host.Definition(IndependentChildMaterialization.ProcessName);
        var closure = host.Definition(PolicyCampaignClosureEffects.ProcessName);

        // Assert
        Assert.False(results.Ordered);
        Assert.Contains(nameof(PolicyParticipantResultAdmitted), results.RootSourceEvents);
        Assert.Contains(nameof(ApplyPolicyParticipantResult), results.Stages.Single().Receivers);
        Assert.True(closure.Ordered);
        Assert.Equal(["acquire", "complete", "settle"], closure.Stages.Select(static stage => stage.Stage));
        Assert.Equal([nameof(AbortPolicyCampaignClosure)], closure.Abort);
    }
}
