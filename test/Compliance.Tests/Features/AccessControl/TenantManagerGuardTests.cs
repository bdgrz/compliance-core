using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

/// <summary>
///     The tenant manager guard as the first owner of every administrator-changing route (#1022): trace LOCK-08 step
///     by step, the last-administrator rule under concurrency, route-determined admission, exact retries and the
///     bootstrap of the contribution set from an authoritative source cut.
/// </summary>
public sealed class TenantManagerGuardTests
{
    [Fact]
    public async Task ShouldReplayFrozenAcceptanceGivenSystemRemovalRetryAfterAssignmentClosed()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        _ = await host.AddAdministratorAsync();
        var target = await host.AddAdministratorAsync();
        var request = TeamMemberEffects.Remove(Uuid.CreateVersion4(), host.TenantId,
            host.AdministratorsTeamId, target.MemberId);
        var first = await host.SubmitSystemRemovalAsync(request);
        Assert.True(first.IsSuccess);
        Assert.Equal(ProcessState.Completed, (await host.DriveAsync(request.CommandId)).State);

        // Act
        var retry = await host.SubmitSystemRemovalAsync(request);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value, retry.Value);
        Assert.False((await host.AssignmentAsync(target)).IsAssigned);
    }

    [Fact]
    public async Task ShouldReplayOriginalEpisodeGivenSystemAssignmentRetryAfterReRegistration()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var actor = await host.AddAdministratorAsync();
        var target = await host.AddMemberAsync();
        var request = TeamMemberEffects.Assign(Uuid.CreateVersion4(), host.TenantId,
            host.AdministratorsTeamId, target.MemberId);
        var original = await host.SubmitSystemAssignmentAsync(request);
        Assert.True(original.IsSuccess);
        Assert.Equal(ProcessState.Completed, (await host.DriveAsync(request.CommandId)).State);
        var retired = await host.DeprovisionAsync(actor, target);
        Assert.True(retired.IsSuccess);
        Assert.Equal(ProcessState.Completed, (await host.DriveAsync(retired.Value!.OperationId)).State);
        var proof = (await host.MemberAsync(target)).TerminationProof!;
        var cleanup = new MemberAuthorityCleanup(host.Reader, host.Provider.GetRequiredService<IAggregateExecutor>(),
            host.Events, new InMemoryProjectionCheckpointStore(), TimeProvider.System);
        Assert.True((await cleanup.CompleteAsync(new RequestDispatchContext(RequestActor.System), proof,
            CancellationToken.None)).IsSuccess);
        await host.SeedAsync(new Member(host.TenantId, target.UserId), member => member.Register());
        var before = (await host.GuardFactsAsync()).Count;

        // Act
        var retry = await host.SubmitSystemAssignmentAsync(request);
        var changed = await host.SubmitSystemAssignmentAsync(request with { ExpectedAssignmentRevision = 9 });

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(original.Value, retry.Value);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        Assert.NotEqual(target.EpisodeId, (await host.MemberAsync(target)).MembershipEpisodeId);
        Assert.False((await host.AssignmentAsync(target)).IsAssigned);
        Assert.Equal(before, (await host.GuardFactsAsync()).Count);
    }

    [Theory]
    [InlineData(false, "episode")]
    [InlineData(false, "revision")]
    [InlineData(false, "generation")]
    [InlineData(true, "episode")]
    [InlineData(true, "revision")]
    [InlineData(true, "generation")]
    public async Task ShouldAppendNoGuardFactGivenStaleAdministratorAssignment(bool remove, string change)
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var actor = await host.AddAdministratorAsync();
        var target = await host.AddAdministratorAsync();
        if (!remove)
        {
            var accepted = await host.RemoveAsync(actor, target);
            Assert.True(accepted.IsSuccess);
            Assert.Equal(ProcessState.Completed, (await host.DriveAsync(accepted.Value!.OperationId)).State);
        }
        var assign = await host.AssignRequestAsync(target);
        var removal = await host.RemoveRequestAsync(target);
        IRequestBase request = remove ? change switch
        {
            "episode" => removal with { MemberEpisodeId = Uuid.CreateVersion4() },
            "revision" => removal with { ExpectedAssignmentRevision = removal.ExpectedAssignmentRevision + 1 },
            _ => removal with { ExpectedAssignmentGeneration = removal.ExpectedAssignmentGeneration + 1 },
        } : change switch
        {
            "episode" => assign with { MemberEpisodeId = Uuid.CreateVersion4() },
            "revision" => assign with { ExpectedAssignmentRevision = assign.ExpectedAssignmentRevision + 1 },
            _ => assign with { ExpectedAssignmentGeneration = assign.ExpectedAssignmentGeneration + 1 },
        };
        var before = (await host.GuardFactsAsync()).Count;

        // Act
        var result = await host.SubmitAsync(actor, request);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(before, (await host.GuardFactsAsync()).Count);
        Assert.Equal(remove, (await host.AssignmentAsync(target)).IsAssigned);
    }

    [Fact]
    public async Task ShouldWaitForWithdrawalGivenAdministratorRestorationArrivesBeforeSourceApply()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());
        var withdrawalId = Uuid.CreateVersion4();
        var restorationId = Uuid.CreateVersion4();

        // Act
        var withdrawal = await host.SubmitAsync(second, await host.SuspendRequestAsync(first, withdrawalId));
        var availableAfterWithdrawal = await host.IsAvailableAsync(first);
        var suspendedBeforeApply = (await host.MemberAsync(first)).IsSuspended;
        var restoration = await host.SubmitAsync(second, await host.ReinstateRequestAsync(first, restorationId));
        var waiting = await host.DriveAsync(restorationId);
        var guardFactsWhileWaiting = await host.GuardFactsAsync();
        var suspendedWhileWaiting = (await host.MemberAsync(first)).IsSuspended;
        var availableWhileWaiting = await host.IsAvailableAsync(first);
        var applied = await host.DriveAsync(withdrawalId);
        var suspendedAfterWithdrawal = (await host.MemberAsync(first)).IsSuspended;
        var resumed = await host.ResumeAsync(restorationId);
        var guardFacts = await host.GuardFactsAsync();

        // Assert
        Assert.Equal(AdministratorTransitionResponseState.Accepted, withdrawal.Value!.State);
        Assert.Equal(AdministratorTransitionResponseState.Accepted, restoration.Value!.State);
        Assert.False(availableAfterWithdrawal);
        Assert.False(suspendedBeforeApply);
        Assert.Equal(ProcessState.Pending, waiting.State);
        Assert.StartsWith(AdministratorReceivers.ApplyMemberRestoration, waiting.Note, StringComparison.Ordinal);
        Assert.Equal(2, guardFactsWhileWaiting.Count);
        Assert.False(suspendedWhileWaiting);
        Assert.False(availableWhileWaiting);
        Assert.Equal(ProcessState.Completed, applied.State);
        Assert.True(suspendedAfterWithdrawal);
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.False((await host.MemberAsync(first)).IsSuspended);
        Assert.True(await host.IsAvailableAsync(first));
        Assert.Collection(guardFacts,
            fact => Assert.IsType<TenantManagerWithdrawalAccepted>(fact),
            fact => Assert.IsType<TenantManagerRestorationAccepted>(fact),
            fact => Assert.IsType<TenantManagerWithdrawalCompleted>(fact),
            fact => Assert.IsType<TenantManagerRestored>(fact),
            fact => Assert.IsType<TenantManagerRestorationCompleted>(fact));
        var memberFacts = await host.FactsAsync((await host.MemberAsync(first)).Stream);
        Assert.IsType<MemberSuspended>(memberFacts[1]);
        Assert.IsType<MemberReinstated>(memberFacts[2]);
    }

    [Fact]
    public async Task ShouldClearOnlyMatchingReasonGivenRestorationCompletesWithOtherReasons()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());
        await DriveAsync(host, await host.SubmitAsync(second, await host.SuspendRequestAsync(first)));
        await DriveAsync(host, await host.SubmitAsync(second, await host.RemoveRequestAsync(first)));
        var reasonsBefore = (await host.GuardAsync()).ReasonsFor(first.EpisodeId).Order().ToArray();

        // Act
        var reinstated = await host.SubmitAsync(second, await host.ReinstateRequestAsync(first));
        await host.DriveAsync(reinstated.Value!.OperationId);
        var afterReinstate = await host.GuardAsync();
        var factsAfterReinstate = await host.GuardFactsAsync();
        var assigned = await host.SubmitAsync(second, await host.AssignRequestAsync(first));
        await host.DriveAsync(assigned.Value!.OperationId);
        var afterAssign = await host.GuardAsync();

        // Assert
        Assert.Equal([TenantManagerGuard.RemovedFromAdministrators, TenantManagerGuard.Suspended], reasonsBefore);
        Assert.Equal([TenantManagerGuard.RemovedFromAdministrators], afterReinstate.ReasonsFor(first.EpisodeId));
        Assert.False(afterReinstate.IsAvailable(first.EpisodeId));
        Assert.IsType<TenantManagerRestorationCompleted>(factsAfterReinstate[^1]);
        Assert.DoesNotContain(factsAfterReinstate, fact => fact is TenantManagerRestored);
        Assert.Empty(afterAssign.ReasonsFor(first.EpisodeId));
        Assert.True(afterAssign.IsAvailable(first.EpisodeId));
        Assert.Single((await host.GuardFactsAsync()).OfType<TenantManagerRestored>());
        Assert.True((await host.AssignmentAsync(first)).IsAssigned);
    }

    [Fact]
    public async Task ShouldIgnoreOldEpisodeCallbackGivenNewEpisode()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());
        var withdrawal = await host.SubmitAsync(second, await host.SuspendRequestAsync(first));
        var reregistered = await ReRegisterAsync(host, first);

        // Act
        var record = await host.DriveAsync(withdrawal.Value!.OperationId);
        var guard = await host.GuardAsync();

        // Assert
        Assert.Equal(ProcessState.Completed, record.State);
        Assert.Contains("superseded", record.Note, StringComparison.Ordinal);
        Assert.False((await host.MemberAsync(first)).IsSuspended);
        Assert.Equal(reregistered, (await host.MemberAsync(first)).MembershipEpisodeId);
        Assert.False(guard.HasContribution(reregistered));
        Assert.DoesNotContain(await host.GuardFactsAsync(), fact => fact is TenantManagerWithdrawalCompleted);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldFenceSuspensionGivenPendingAdministratorRegistrationForSameEpisode(bool registrationFirst)
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var first = await host.AddAdministratorAsync();
        await host.AddAdministratorAsync();
        var candidate = await host.AddMemberAsync();
        Uuid registrationId;
        Uuid suspensionId;

        // Act
        if (registrationFirst)
        {
            registrationId = (await host.SubmitAsync(first, await host.AssignRequestAsync(candidate))).Value!.OperationId;
            suspensionId = (await host.SubmitAsync(first, await host.SuspendRequestAsync(candidate))).Value!.OperationId;
        }
        else
        {
            suspensionId = (await host.SubmitAsync(first, await host.SuspendRequestAsync(candidate))).Value!.OperationId;
            registrationId = (await host.SubmitAsync(first, await host.AssignRequestAsync(candidate))).Value!.OperationId;
        }

        var registration = await host.IntentAsync(registrationId);
        var suspension = await host.IntentAsync(suspensionId);
        var pendingReasons = (await host.GuardAsync()).ReasonsFor(candidate.EpisodeId);
        await host.DriveAsync(registrationId);
        await host.DriveAsync(suspensionId);
        var guard = await host.GuardAsync();
        var facts = await host.GuardFactsAsync();

        // Assert
        Assert.Equal(AdministratorTransitionKind.RegisterAdministratorsAssignment, registration.TransitionKind);
        Assert.Equal(registrationFirst ? AdministratorTransitionKind.SuspendMember
            : AdministratorTransitionKind.NeutralMemberMutation, suspension.TransitionKind);
        Assert.Contains(TenantManagerGuard.Suspended, pendingReasons);
        Assert.True((await host.MemberAsync(candidate)).IsSuspended);
        Assert.True((await host.AssignmentAsync(candidate)).IsAssigned);
        Assert.True(guard.HasContribution(candidate.EpisodeId));
        Assert.False(guard.IsAvailable(candidate.EpisodeId));
        Assert.DoesNotContain(facts, fact => fact is TenantManagerRestored);
        Assert.Contains(facts, fact => fact is TenantManagerRestorationCompleted);
        Assert.Equal(2, guard.AvailableCount);
    }

    [Fact]
    public async Task ShouldAppendDirectlyWithoutGuardReadGivenNonAdministratorTeamRemoval()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var admin = await host.AddAdministratorAsync();
        var member = await host.AddMemberAsync();
        var team = Uuid.CreateVersion4();
        await host.SeedAsync(new TeamMember(host.TenantId, team, member.MemberId),
            assignment => assignment.Assign(member.EpisodeId));
        var readsBefore = host.GuardReads;

        // Act
        var removed = await host.SubmitAsync(admin, await host.RemoveRequestAsync(member, team));

        // Assert
        Assert.Equal(AdministratorTransitionResponseState.Completed, removed.Value!.State);
        Assert.False((await host.AssignmentAsync(member, team)).IsAssigned);
        Assert.Equal(readsBefore, host.GuardReads);
        Assert.Empty(await host.GuardFactsAsync());
    }

    [Fact]
    public async Task ShouldAdmitNeutralFactGivenNonAdministratorSuspension()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var first = await host.AddAdministratorAsync();
        await host.AddAdministratorAsync();
        var member = await host.AddMemberAsync();

        // Act
        var accepted = await host.SubmitAsync(first, await host.SuspendRequestAsync(member));
        var factsBefore = await host.GuardFactsAsync();
        var suspendedBeforeApply = (await host.MemberAsync(member)).IsSuspended;
        var record = await host.DriveAsync(accepted.Value!.OperationId);
        var factsAfter = await host.GuardFactsAsync();
        var suspension = Assert.IsType<MemberSuspended>((await host.FactsAsync((await host.MemberAsync(member)).Stream))[1]);

        // Assert
        Assert.Equal(AdministratorTransitionResponseState.Accepted, accepted.Value.State);
        var admitted = Assert.IsType<TenantManagerSourceMutationAdmitted>(Assert.Single(factsBefore));
        Assert.NotNull(admitted.SourceProofs);
        Assert.False(suspendedBeforeApply);
        Assert.Equal(ProcessState.Completed, record.State);
        Assert.Equal([AdministratorReceivers.ApplyNeutralMemberSourceMutation], record.Facts.Keys);
        Assert.Single(factsAfter);
        Assert.NotNull(suspension.OperationResult);
        Assert.Equal(admitted.Operation.RootOperationId, suspension.Operation.RootOperationId);
        Assert.Equal(2, (await host.GuardAsync()).AvailableCount);
    }

    [Fact]
    public async Task ShouldKeepOneActiveAdministratorGivenConcurrentMutualSuspension()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());
        var firstRequest = await host.SuspendRequestAsync(second);
        var secondRequest = await host.SuspendRequestAsync(first);
        host.HoldGuardReads(2);

        // Act
        var results = await Task.WhenAll(host.SubmitAsync(first, firstRequest), host.SubmitAsync(second, secondRequest));
        var guard = await host.GuardAsync();

        // Assert
        Assert.Single(results, result => result.IsSuccess);
        var denied = Assert.Single(results, result => !result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, denied.Error!.Kind);
        Assert.False(denied.Error.IsTransient);
        Assert.Equal(1, guard.AvailableCount);
        Assert.Single((await host.GuardFactsAsync()).OfType<TenantManagerWithdrawalAccepted>());
    }

    [Fact]
    public async Task ShouldRejectWithdrawingLastAdministratorGivenSequentialSuspension()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());

        // Act
        var self = await host.SuspendAsync(first, first);
        var suspended = await host.SuspendAsync(first, second);
        var retaliation = await host.SuspendAsync(second, first);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, self.Error!.Kind);
        Assert.True(suspended.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, retaliation.Error!.Kind);
        Assert.False(retaliation.Error.IsTransient);
        Assert.Equal(1, (await host.GuardAsync()).AvailableCount);
    }

    [Fact]
    public async Task ShouldRejectRemovingLastAdministratorGivenAdministratorsTeam()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var only = await host.AddAdministratorAsync();

        // Act
        var removed = await host.RemoveAsync(only, only);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, removed.Error!.Kind);
        Assert.Same(TenantManagerGuard.LastAdministratorConflict, removed.Error);
        Assert.True((await host.AssignmentAsync(only)).IsAssigned);
    }

    [Fact]
    public async Task ShouldRestoreWitnessGivenReinstatedAdministrator()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());
        await DriveAsync(host, await host.SuspendAsync(first, second));

        // Act
        await DriveAsync(host, await host.ReinstateAsync(first, second));
        var reversed = await host.SuspendAsync(second, first);
        await DriveAsync(host, reversed);

        // Assert
        Assert.True(reversed.IsSuccess);
        Assert.True((await host.MemberAsync(first)).IsSuspended);
        Assert.False((await host.MemberAsync(second)).IsSuspended);
        Assert.True(await host.IsAvailableAsync(second));
    }

    [Fact]
    public async Task ShouldRemoveAndReassignAdministratorGivenAnotherActiveAdministrator()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());

        // Act
        var removed = await host.RemoveAsync(first, second);
        await DriveAsync(host, removed);
        var lastRemoval = await host.RemoveAsync(first, first);
        var reassigned = await host.AssignAsync(first, second);
        await DriveAsync(host, reassigned);
        var nowRemovable = await host.RemoveAsync(second, first);
        await DriveAsync(host, nowRemovable);

        // Assert
        Assert.True(removed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, lastRemoval.Error!.Kind);
        Assert.True(reassigned.IsSuccess);
        Assert.True(nowRemovable.IsSuccess);
        Assert.False((await host.AssignmentAsync(first)).IsAssigned);
        Assert.True((await host.AssignmentAsync(second)).IsAssigned);
        Assert.True(await host.IsAvailableAsync(second));
        Assert.Equal(1, (await host.GuardAsync()).AvailableCount);
    }

    [Fact]
    public async Task ShouldRegisterFirstAdministratorGivenEmptyTenant()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var owner = await host.AddMemberAsync();

        // Act
        var accepted = await host.SubmitAsync(owner, await host.AssignRequestAsync(owner));
        var intent = await host.IntentAsync(accepted.Value!.OperationId);
        var before = await host.GuardAsync();
        var record = await host.DriveAsync(accepted.Value.OperationId);
        var after = await host.GuardAsync();

        // Assert
        Assert.Equal(AdministratorTransitionKind.RegisterAdministratorsAssignment, intent.TransitionKind);
        Assert.Equal(1UL, intent.AssignmentGeneration);
        Assert.False(before.IsAvailable(owner.EpisodeId));
        Assert.Equal(ProcessState.Completed, record.State);
        Assert.Equal([AdministratorReceivers.ApplyAdministratorAssignmentRestoration,
            AdministratorReceivers.CompleteAdministratorRestoration], record.Facts.Keys.Order().ToArray());
        Assert.True(after.IsAvailable(owner.EpisodeId));
        Assert.True((await host.AssignmentAsync(owner)).IsAssigned);
        Assert.Contains(await host.GuardFactsAsync(), fact => fact is TenantManagerRestored restored &&
                                                                restored.Reason == TenantManagerGuard.Registered);
    }

    [Theory]
    [InlineData("suspend")]
    [InlineData("deprovision")]
    [InlineData("reinstate")]
    [InlineData("register")]
    [InlineData("remove")]
    public async Task ShouldReplayFrozenResponseGivenExactRetryOfEachPublicCommand(string command)
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());
        var candidate = await host.AddMemberAsync();
        if (command == "reinstate")
            await DriveAsync(host, await host.SuspendAsync(first, second));
        IRequestBase request = command switch
        {
            "suspend" => await host.SuspendRequestAsync(second),
            "deprovision" => await host.DeprovisionRequestAsync(second),
            "reinstate" => await host.ReinstateRequestAsync(second),
            "register" => await host.AssignRequestAsync(candidate),
            _ => await host.RemoveRequestAsync(second),
        };
        var original = await host.SubmitAsync(first, request);
        await host.DriveAsync(original.Value!.OperationId);
        var factsBefore = (await host.GuardFactsAsync()).Count;

        // Act
        var retry = await host.SubmitAsync(first, request);

        // Assert
        Assert.Equal(AdministratorTransitionResponseState.Accepted, original.Value.State);
        Assert.Equal(original.Value, retry.Value);
        Assert.Equal(factsBefore, (await host.GuardFactsAsync()).Count);
    }

    [Theory]
    [InlineData("reason")]
    [InlineData("actor")]
    [InlineData("revision")]
    public async Task ShouldConflictGivenSameCommandIdWithChangedIntent(string change)
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());
        var third = await host.AddAdministratorAsync();
        var commandId = Uuid.CreateVersion4();
        var request = await host.SuspendRequestAsync(second, commandId);
        Assert.True((await host.SubmitAsync(first, request)).IsSuccess);

        // Act
        var retry = await host.SubmitAsync(change == "actor" ? third : first, change switch
        {
            "reason" => request with { Reason = "A different reason." },
            "revision" => request with { ExpectedMemberRevision = request.ExpectedMemberRevision + 1 },
            _ => request,
        });

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, retry.Error!.Kind);
        Assert.Single((await host.GuardFactsAsync()).OfType<TenantManagerWithdrawalAccepted>());
    }

    [Fact]
    public async Task ShouldReplayOriginalResponseGivenExactRetryAfterLaterLifecycle()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());
        var request = await host.SuspendRequestAsync(second);
        var original = await host.SubmitAsync(first, request);
        await host.DriveAsync(original.Value!.OperationId);
        await DriveAsync(host, await host.ReinstateAsync(first, second));
        await DriveAsync(host, await host.DeprovisionAsync(first, second));

        // Act
        var retry = await host.SubmitAsync(first, request);

        // Assert
        Assert.Equal(original.Value, retry.Value);
        Assert.Equal(1, (await host.GuardFactsAsync()).OfType<TenantManagerWithdrawalAccepted>().Count(
            fact => fact.Operation.LogicalId == request.CommandId));
    }

    [Fact]
    public async Task ShouldBuildContributionsFromSourceCutGivenEmptyGuardStream()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var available = await host.AddAdministratorAsync();
        var suspended = await host.AddAdministratorAsync();
        await host.SeedAsync(new Member(host.TenantId, suspended.UserId), member =>
            member.Suspend(Uuid.CreateVersion4(), "Alex Admin", AdministratorTransitionHost.At, "Employment ended."));
        var stale = await host.AddAdministratorAsync();
        var elsewhere = await host.AddMemberAsync();
        await host.SeedAsync(new TeamMember(host.TenantId, Uuid.CreateVersion4(), elsewhere.MemberId),
            assignment => assignment.Assign(elsewhere.EpisodeId));
        var bootstrap = new TenantManagerGuardBootstrap(host.Events, host.Reader, TimeProvider.System);

        // Act
        var baseline = await bootstrap.BuildAsync(host.TenantId, CancellationToken.None);
        var guard = new TenantManagerGuard(host.TenantId, baseline: baseline);

        // Assert
        Assert.Equal(3, baseline.Contributions.Count);
        Assert.True(guard.IsAvailable(available.EpisodeId));
        Assert.False(guard.IsAvailable(suspended.EpisodeId));
        Assert.Equal([TenantManagerGuard.Suspended], guard.ReasonsFor(suspended.EpisodeId));
        Assert.True(guard.IsAvailable(stale.EpisodeId));
        Assert.False(guard.HasContribution(elsewhere.EpisodeId));
        Assert.Equal(2, guard.AvailableCount);
        Assert.All(baseline.Contributions, contribution => Assert.Equal(1, contribution.AssignmentGeneration));
    }

    [Fact]
    public async Task ShouldPinSourceCutOnFirstFactGivenFirstAdmission()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());

        // Act
        var accepted = await host.SubmitAsync(first, await host.SuspendRequestAsync(second));
        var later = await host.SubmitAsync(second, await host.SuspendRequestAsync(first));
        var facts = (await host.GuardFactsAsync()).OfType<ITenantManagerGuardFact>().ToArray();
        var baseline = await host.Content.ReadJsonAsync<GuardBaseline>(host.TenantId.ToString(), facts[0].SourceProofs!);

        // Assert
        Assert.True(accepted.IsSuccess);
        Assert.False(later.IsSuccess);
        Assert.Single(facts);
        Assert.Equal(new[] { first.EpisodeId, second.EpisodeId }.Order(),
            baseline.Contributions.Select(c => c.MemberEpisodeId).Order());
        Assert.Equal(1L, facts[0].Generation);
    }

    [Fact]
    public async Task ShouldRejectDismantlingBuiltInAdministrationGivenRoleOrPermissionRemoval()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var executor = host.Provider.GetRequiredService<IAggregateExecutor>();
        var tenantId = host.TenantId;
        var team = host.AdministratorsTeamId;
        var role = BuiltInRbac.TenantAdministrationRoleId(tenantId);
        var actor = AdministratorTransitionHost.Actor(await host.AddAdministratorAsync());

        // Act
        var removeRole = await new RemoveTeamRoleHandler(executor).HandleAsync(new RequestContext<RemoveTeamRole>(
            new RemoveTeamRole(tenantId, team, role, Uuid.CreateVersion4(), 1,
                RbacTeamRoleFacts.AssignmentId(tenantId, team, role)), actor), CancellationToken.None);
        var removeManage = await new RemoveRolePermissionHandler(executor).HandleAsync(
            new RequestContext<RemoveRolePermission>(new RemoveRolePermission(tenantId, role,
                RbacPermissions.TenantRbacManage, Uuid.CreateVersion4(), 1,
                RbacPermissionFacts.AssignmentId(tenantId, role, RbacPermissions.TenantRbacManage)), actor),
            CancellationToken.None);
        var removeAccess = await new RemoveRolePermissionHandler(executor).HandleAsync(
            new RequestContext<RemoveRolePermission>(new RemoveRolePermission(tenantId, role,
                RbacPermissions.TenantAccess, Uuid.CreateVersion4(), 1,
                RbacPermissionFacts.AssignmentId(tenantId, role, RbacPermissions.TenantAccess)), actor),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, removeRole.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, removeManage.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, removeAccess.Error!.Kind);
    }

    static async Task DriveAsync(AdministratorTransitionHost host, Result<AdministratorTransitionResponse> admitted)
    {
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        var record = await host.DriveAsync(admitted.Value!.OperationId);
        Assert.Equal(ProcessState.Completed, record.State);
    }

    /// <summary>Retires the member's episode as retirement does and registers a new one; returns the new episode.</summary>
    static async Task<Uuid> ReRegisterAsync(AdministratorTransitionHost host, AdministratorTransitionHost.Person person)
    {
        await host.SeedAsync(new Member(host.TenantId, person.UserId), member =>
            member.Deprovision(person.MemberId, "Alex Admin", AdministratorTransitionHost.At, "Access ended."));
        var proof = Assert.IsType<MembershipTerminationProof>((await host.MemberAsync(person)).TerminationProof);
        var assignment = await host.AssignmentAsync(person);
        await host.SeedAsync(new TeamMember(host.TenantId, host.AdministratorsTeamId, person.MemberId),
            owner => owner.RemoveForRetirement(proof, assignment.AssignmentEventId, Uuid.CreateVersion4(),
                AdministratorTransitionHost.At));
        await host.SeedAsync(new Member(host.TenantId, person.UserId, proof.MembershipEpisodeId), member =>
            member.CompleteDeprovisionCleanup(proof, MemberRetirementEffects.Complete(proof).CommandId,
                AdministratorTransitionHost.At.AddMinutes(1)));
        await host.SeedAsync(new Member(host.TenantId, person.UserId), member => member.Register());
        return (await host.MemberAsync(person)).MembershipEpisodeId;
    }
}
