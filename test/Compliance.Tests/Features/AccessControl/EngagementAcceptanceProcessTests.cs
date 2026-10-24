using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using static Bdgrz.Compliance.Tests.Testing.AcceptanceFacts;
using static Bdgrz.Compliance.Tests.Testing.PersonalDecisionFacts;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class EngagementAcceptanceProcessTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ShouldSettleEveryKeyGivenAcceptanceRejectedBeforeProcessCheckpoint(int stageIndex)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await ReadyAsync(host);
        var request = AcceptCommand(host, engagement);
        Assert.True((await host.HttpAsync(request, partner.Actor)).IsSuccess);
        var record = await StartAsync(host, engagement, request.CommandId);
        var facts = new Dictionary<string, NativeFactReference>();
        foreach (var target in record.Targets)
            foreach (var receiver in EngagementAcceptanceProcess.Definition().Stages[0].Receivers)
            {
                if (await BuildAsync(host, record, receiver, target) is not { } command)
                    continue;
                var held = Assert.IsType<ReceiverOutcome.Applied>((await SendAsync(host, command)).Value);
                facts[ProcessRecord.FactKey(receiver, target)] = held.Fact;
            }
        record = await host.Scope.ServiceProvider.GetRequiredService<IProcessRecordStore>().ReplaceAsync(
            record with { StageIndex = stageIndex, Facts = facts }, record.Version);
        var reject = (await BuildAsync(host, record, nameof(RejectServiceEngagementAcceptance)))!;
        Assert.IsType<ReceiverOutcome.Applied>((await SendAsync(host, reject)).Value);
        Assert.True((await host.HydrateAsync(new ClientServiceIndependence(host.Tenant))).Uses.HasActiveUse);

        // Act
        var resumed = await RunAsync(host, engagement, request.CommandId);

        // Assert
        Assert.False((await host.HydrateAsync(new ClientServiceIndependence(host.Tenant))).Uses.HasActiveUse);
        var intent = await IntentOfAsync(host, engagement, request.CommandId);
        foreach (var subject in await host.Content.ReadSelectionAsync<PracticeSubjectIntent>(host.Tenant.ToString(),
                     intent.Subjects))
            Assert.False((await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant, subject.Kind,
                subject.SubjectId))).Uses.HasActiveUse);
        Assert.Equal(ProcessState.Aborted, resumed.State);
    }

    [Fact]
    public async Task ShouldAcquireCanonicalOrderGivenEpochKeySortsAfterPracticeKeys()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host);
        var (first, _) = await SeedEngagementAsync(host);
        await ProposeSecondAsync(host, first);
        var operation = await AdmitAsync(host, first, partner, 2);
        var targets = await TargetsAsync(host, first, operation);
        var epochKey = PracticeSubjects.Key(new ClientServiceIndependence(host.Tenant).Stream);

        // Act
        var observed = new ObservedAcceptanceProcess();
        var (record, crash) = await observed.RunAsync(host, first, operation);
        var intent = await IntentOfAsync(host, first, operation);
        var requirements = await host.Content.ReadSelectionAsync<SourceUseRequirement>(host.Tenant.ToString(),
            intent.SourceUses);
        var subjects = await host.Content.ReadSelectionAsync<PracticeSubjectIntent>(host.Tenant.ToString(),
            intent.Subjects);
        var acquired = observed.Sent.TakeWhile(item => item.Receiver != nameof(AuthorizeEngagementPracticeCommitments))
            .Select(item => PracticeSubjects.Key(item.Target!)).ToArray();

        // Assert: the frozen manifests are canonical, and a practice key sorts before the service epoch key.
        Assert.Null(crash);
        Assert.Equal(ProcessState.Completed, record!.State);
        Assert.Equal(targets.Order(StreamKeyOrder.Instance), targets);
        Assert.Equal(requirements.Select(item => PracticeSubjects.Key(item.SourceOwner))
            .Order(StreamKeyOrder.Instance), requirements.Select(item => PracticeSubjects.Key(item.SourceOwner)));
        Assert.Equal(subjects.Select(item => item.Key).Order(StreamKeyOrder.Instance), subjects.Select(item => item.Key));
        Assert.Contains(targets, key => key.Contains("/client-practice-commitments/", StringComparison.Ordinal) &&
            StreamKeyOrder.Instance.Compare(key, epochKey) < 0);

        // Assert: every key was acquired exactly once, in that one global order, whichever receiver owns it.
        Assert.Equal(targets, acquired);
        var practiceKeys = acquired.Where(key => key.Contains("/client-practice-commitments/",
            StringComparison.Ordinal)).ToArray();
        Assert.Equal(4, practiceKeys.Length);

        // The epoch receiver comes first in the catalog, yet its key is acquired after the practice keys: the list of
        // receivers is not an acquisition order, the manifest is.
        Assert.All(practiceKeys, key => Assert.True(Array.IndexOf(acquired, key) < Array.IndexOf(acquired, epochKey)));
        Assert.Equal(nameof(AcquireEngagementServiceEpoch), EngagementAcceptanceProcess.Definition().Stages[0].Receivers[0]);
    }

    [Fact]
    public async Task ShouldAcquireCanonicalOrderGivenReversedAuthoredManifestAndOverlappingOperation()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host);
        var (first, _) = await SeedEngagementAsync(host);
        await ProposeSecondAsync(host, first);
        var one = await AdmitAsync(host, first, partner, 2);
        var intent = await IntentOfAsync(host, first, one);
        var requirements = await host.Content.ReadSelectionAsync<SourceUseRequirement>(host.Tenant.ToString(),
            intent.SourceUses);
        var subjects = await host.Content.ReadSelectionAsync<PracticeSubjectIntent>(host.Tenant.ToString(),
            intent.Subjects);

        // Act
        var forward = EngagementAcceptanceProcess.Targets(requirements, subjects);
        var reversed = EngagementAcceptanceProcess.Targets(requirements.Reverse(), subjects.Reverse());
        var authoredKeys = requirements.Select(item => PracticeSubjects.Key(item.SourceOwner)).Concat(
            subjects.Select(item => item.Key)).Reverse().ToArray();
        var admitted = await OperationAsync(host, first, one);
        var source = NativeFactReference.Of(new ServiceEngagementIndependence(host.Tenant, first).Stream,
            admitted.Requested);
        var record = ProcessRecord.Start(AcceptanceFacts.KeyOf(one, source), source, "initial",
            admitted.Intent, authoredKeys, DateTimeOffset.UtcNow);
        var observedOne = new ObservedAcceptanceProcess();
        await observedOne.RunAsync(host, first, one);
        var second = await CreateEngagementAsync(host);
        var overlapping = await AdmitAsync(host, second, partner, 1, evaluate: true);
        var observedTwo = new ObservedAcceptanceProcess();
        var (finished, crash) = await observedTwo.RunAsync(host, second, overlapping);

        // Assert
        Assert.Equal(forward, reversed);
        Assert.Equal(forward, record.Targets);
        Assert.Null(crash);
        Assert.Equal(ProcessState.Completed, finished!.State);
        var firstOrder = KeyOrder(observedOne);
        var secondOrder = KeyOrder(observedTwo);
        Assert.Equal(firstOrder.Order(StreamKeyOrder.Instance), firstOrder);
        Assert.Equal(secondOrder.Order(StreamKeyOrder.Instance), secondOrder);
        var shared = firstOrder.Intersect(secondOrder).ToHashSet();
        Assert.NotEmpty(shared);
        Assert.Equal(firstOrder.Where(shared.Contains), secondOrder.Where(shared.Contains));
    }

    [Fact]
    public async Task ShouldReserveBeforeEpochGrantAndFenceEveryKeyGivenServiceEpochAdvancedAndOperationRejected()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await ReadyAsync(host);
        var command = AcceptCommand(host, engagement);
        Assert.True((await host.HttpAsync(command, partner.Actor)).IsSuccess);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;

        // Act: pause with only some practice keys reserved, before the service epoch grant.
        var paused = new ObservedAcceptanceProcess(stopBeforeReceiver: nameof(AcquireEngagementServiceEpoch));
        var (_, pause) = await paused.RunAsync(host, engagement, command.CommandId);
        var reservedBeforeEpoch = paused.Sent.Where(item => item.Receiver == nameof(ReservePracticeIndependence)).ToArray();
        var advanced = await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 1,
            IndependenceHost.Service()));
        var afterEpoch = await RunAsync(host, engagement, command.CommandId);

        // Assert: no permanent wall exists, the stale epoch was refused and nothing was authorized.
        Assert.NotNull(pause);
        Assert.NotEmpty(reservedBeforeEpoch);
        Assert.True(advanced.IsSuccess, advanced.Error?.Message);
        Assert.Equal(ProcessState.Aborted, afterEpoch.State);
        Assert.Single(await host.EventsAsync<ServiceEngagementAcceptanceRejected>(stream));
        Assert.Empty(await host.EventsAsync<EngagementPracticeCommitmentsAuthorized>(stream));
        var service = await host.HydrateAsync(new ClientServiceIndependence(host.Tenant));
        var epochKey = EngagementAcceptanceEffects.UseKey(command.CommandId, stream, service.Stream);
        Assert.True(service.Uses.Find(epochKey) is { IsFence: true }, "the epoch key must be fenced, never granted");

        // Assert: every practice key is settled, reserved or not, and none ever committed.
        var intent = await IntentOfAsync(host, engagement, command.CommandId);
        foreach (var subject in await host.Content.ReadSelectionAsync<PracticeSubjectIntent>(host.Tenant.ToString(),
                     intent.Subjects))
        {
            var owner = await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant, subject.Kind,
                subject.SubjectId));
            Assert.Null(owner.Practice);
            Assert.True(owner.Uses.Find(EngagementAcceptanceEffects.UseKey(command.CommandId, stream, owner.Stream))
                is { IsSettled: true });
        }

        // Act: delayed acquisitions of the rejected operation, including keys never acquired, cannot reactivate it.
        var record = await StartAsync(host, engagement, command.CommandId);
        var late = new List<Result<ReceiverOutcome>>();
        foreach (var target in record.Targets)
            foreach (var receiver in new[]
                     {
                         nameof(AcquireEngagementServiceEpoch), nameof(AuthorizeFirmStaffRecordEngagementUse),
                         nameof(AuthorizeFirmProfessionalDutyDesignationEngagementUse),
                         nameof(AuthorizeIndependenceRuleCatalogEngagementUse),
                         nameof(AuthorizeIndependenceRuleRatificationCatalogEngagementUse),
                         nameof(ReservePracticeIndependence),
                     })
                if (await BuildAsync(host, record, receiver, target) is { } built)
                    late.Add(await SendAsync(host, built));

        // Assert
        Assert.NotEmpty(late);
        Assert.All(late, outcome => Assert.IsType<ReceiverOutcome.Rejected>(outcome.Value));
    }

    [Fact]
    public async Task ShouldAuthorizeOnlyAfterEveryExactGrantAndRecoverForwardGivenFreshOperation()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await ReadyAsync(host);
        var rejected = AcceptCommand(host, engagement);
        Assert.True((await host.HttpAsync(rejected, partner.Actor)).IsSuccess);
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 1,
            ReadinessService()))).IsSuccess);
        Assert.Equal(ProcessState.Aborted, (await RunAsync(host, engagement, rejected.CommandId)).State);

        // Act: a fresh, complete, valid operation after the engagement is re-decided at the new epoch.
        var refused = await host.HttpAsync(AcceptCommand(host, engagement), partner.Actor);
        await PairedDecisionsAsync(host, engagement, partner);
        var fresh = AcceptCommand(host, engagement);
        var admitted = await host.HttpAsync(fresh, partner.Actor);
        var observed = new ObservedAcceptanceProcess();
        var (record, crash) = await observed.RunAsync(host, engagement, fresh.CommandId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.Null(crash);
        Assert.Equal(ProcessState.Completed, record!.State);
        var sent = observed.Sent.Select(item => item.Receiver).ToList();
        var authorize = sent.IndexOf(nameof(AuthorizeEngagementPracticeCommitments));
        Assert.True(authorize > 0);
        Assert.DoesNotContain(sent.Take(authorize), receiver => receiver is nameof(CommitPracticeIndependence) or
            nameof(CompleteServiceEngagementAcceptance));
        Assert.All(sent.Skip(authorize + 1).TakeWhile(receiver => receiver == nameof(CommitPracticeIndependence)),
            receiver => Assert.Equal(nameof(CommitPracticeIndependence), receiver));
        var owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));
        Assert.Equal("accepted", owner.Status);
        Assert.NotNull(owner.OperationOf(fresh.CommandId)!.Completed);
        Assert.NotNull(owner.OperationOf(rejected.CommandId)!.Rejected);
    }

    [Fact]
    public async Task ShouldResumeWithIdenticalCommandIdsAndNoDuplicateGrantGivenCrashMidAcquire()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await ReadyAsync(host);
        var command = AcceptCommand(host, engagement);
        Assert.True((await host.HttpAsync(command, partner.Actor)).IsSuccess);
        var crashing = new ObservedAcceptanceProcess(crashAfterSends: 3);

        // Act: the third receiver applied and the worker crashed before the record advanced.
        var (_, crash) = await crashing.RunAsync(host, engagement, command.CommandId);
        var resumed = new ObservedAcceptanceProcess();
        var (record, secondCrash) = await resumed.RunAsync(host, engagement, command.CommandId);

        // Assert
        Assert.NotNull(crash);
        Assert.Null(secondCrash);
        Assert.Equal(3, crashing.Sent.Count);
        Assert.Equal(ProcessState.Completed, record!.State);
        var replayed = resumed.Sent[0];
        Assert.Equal(crashing.Sent[2].CommandId, replayed.CommandId);
        Assert.Equal(crashing.Sent[2].Target, replayed.Target);
        Assert.Equal(crashing.Sent.Count + resumed.Sent.Count - 1,
            crashing.Sent.Concat(resumed.Sent).Select(item => item.CommandId).Distinct().Count());
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
        var intent = await IntentOfAsync(host, engagement, command.CommandId);
        foreach (var requirement in await host.Content.ReadSelectionAsync<SourceUseRequirement>(host.Tenant.ToString(),
                     intent.SourceUses))
        {
            var owner = await HydrateOwnerAsync(host, requirement.SourceOwner, partner);
            var key = EngagementAcceptanceEffects.UseKey(command.CommandId, stream, requirement.SourceOwner);
            Assert.True(owner.Uses.Find(key) is { GrantFact: not null, SettleFact: not null }, requirement.SourceOwner.Area);
        }

        Assert.Single(await host.EventsAsync<EngagementPracticeCommitmentsAuthorized>(stream));
        Assert.Single(await host.EventsAsync<ServiceEngagementAcceptanceRecorded>(stream));
    }

    [Fact]
    public async Task ShouldFinishForwardAndRefuseRejectionGivenCrashAfterTheIrreversibleAuthorization()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await ReadyAsync(host);
        var command = AcceptCommand(host, engagement);
        Assert.True((await host.HttpAsync(command, partner.Actor)).IsSuccess);
        var stopped = new ObservedAcceptanceProcess(stopBeforeReceiver: nameof(CommitPracticeIndependence));
        var (_, crash) = await stopped.RunAsync(host, engagement, command.CommandId);
        var record = await StartAsync(host, engagement, command.CommandId);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;

        // Act
        var rejection = await SendAsync(host, (await BuildAsync(host, record, nameof(RejectServiceEngagementAcceptance)))!);
        var finished = await RunAsync(host, engagement, command.CommandId);

        // Assert
        Assert.NotNull(crash);
        Assert.Single(await host.EventsAsync<EngagementPracticeCommitmentsAuthorized>(stream));
        Assert.False(rejection.IsSuccess);
        Assert.False(rejection.Error!.IsTransient);
        Assert.Empty(await host.EventsAsync<ServiceEngagementAcceptanceRejected>(stream));
        Assert.Equal(ProcessState.Completed, finished.State);
        Assert.Single(await host.EventsAsync<ServiceEngagementAcceptanceRecorded>(stream));
    }

    static async Task<Uuid> ProposeSecondAsync(IndependenceHost host, Uuid engagement)
    {
        var second = await host.RegisterStaffAsync("attest");
        var proposed = await host.SendAsync(new ProposeServiceEngagementStaff(Uuid.CreateVersion4(), host.Tenant,
            engagement, second.StaffMemberId, 1));
        Assert.True(proposed.IsSuccess, proposed.Error?.Message);
        Assert.Equal(2, proposed.Value!.Revision);
        return second.StaffMemberId;
    }

    static async Task<Uuid> AdmitAsync(IndependenceHost host, Uuid engagement, PartnerSetup partner, long revision,
        bool evaluate = true)
    {
        await PairedDecisionsAsync(host, engagement, partner, revision, evaluate);
        var command = AcceptCommand(host, engagement, revision);
        var admitted = await host.HttpAsync(command, partner.Actor);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        return command.CommandId;
    }

    static async Task<EngagementIndependenceIntent> IntentOfAsync(IndependenceHost host, Uuid engagement, Uuid operation) =>
        await host.Content.ReadJsonAsync<EngagementIndependenceIntent>(host.Tenant.ToString(),
            (await OperationAsync(host, engagement, operation)).Intent);

    static string[] KeyOrder(ObservedAcceptanceProcess observed) => observed.Sent
        .TakeWhile(item => item.Receiver != nameof(AuthorizeEngagementPracticeCommitments))
        .Select(item => PracticeSubjects.Key(item.Target!)).ToArray();

    static async Task<ISourceUseOwner> HydrateOwnerAsync(IndependenceHost host, EventStreamAddress stream,
        PartnerSetup partner)
    {
        switch (stream.Area)
        {
            case ClientServiceIndependence.Area:
                return await host.HydrateAsync(new ClientServiceIndependence(host.Tenant));
            case FirmStaffRecord.Area:
                return await host.HydrateAsync(new FirmStaffRecord(Uuid.Parse(stream.Resource,
                    System.Globalization.CultureInfo.InvariantCulture)));
            case FirmProfessionalDutyDesignation.Area:
                return await host.HydrateAsync(new FirmProfessionalDutyDesignation(partner.Staff.UserId,
                    FirmProfessionalDuty.EngagementPartner, host.Tenant));
            case IndependenceRuleCatalog.Area:
                return await host.HydrateAsync(new IndependenceRuleCatalog());
            case IndependenceRuleRatificationCatalog.Area:
                return await host.HydrateAsync(new IndependenceRuleRatificationCatalog());
            default:
                throw new InvalidOperationException(stream.Area);
        }
    }
}
