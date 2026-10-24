using System.Text.Json;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

/// <summary>
///     The ordered administrator transition process and its eight receivers: the registered definition equals the
///     design catalog, exactly one apply receiver runs per guard fact, and each receiver replays, supersedes, waits
///     or refuses as the receiver contract says.
/// </summary>
public sealed class AdministratorTransitionProcessTests
{
    static readonly string CatalogPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "docs", "product", "audits", "2026-10-23", "target-reactor-contracts.json");

    [Fact]
    public void ShouldEqualDesignCatalogGivenRegisteredAdministratorTransitionProcess()
    {
        // Arrange
        using var document = JsonDocument.Parse(File.ReadAllText(CatalogPath));
        var entry = document.RootElement.GetProperty("processes").EnumerateArray()
            .Single(process => process.GetProperty("process").GetString() == AdministratorTransitionProcess.Name);

        // Act
        var definition = AdministratorTransitionProcess.Definition;

        // Assert
        Assert.True(definition.Ordered);
        Assert.Equal(entry.GetProperty("rootSourceEvents").EnumerateArray().Select(root => root.GetString()),
            definition.RootSourceEvents);
        Assert.Equal(entry.GetProperty("stages").EnumerateArray().Select(stage => stage.GetProperty("stage").GetString()),
            definition.Stages.Select(stage => stage.Stage));
        Assert.Equal(entry.GetProperty("stages").EnumerateArray().Select(stage => stage.GetProperty("receivers")
                .EnumerateArray().Select(receiver => receiver.GetString()).ToArray()),
            definition.Stages.Select(stage => stage.Receivers.ToArray()));
        Assert.Empty(definition.Abort);
        Assert.Empty(entry.GetProperty("abort").EnumerateArray());
    }

    [Theory]
    [InlineData("suspend", new[] { AdministratorReceivers.ApplyMemberWithdrawal, AdministratorReceivers.CompleteAdministratorWithdrawal })]
    [InlineData("remove", new[] { AdministratorReceivers.ApplyAdministratorAssignmentWithdrawal, AdministratorReceivers.CompleteAdministratorWithdrawal })]
    [InlineData("reinstate", new[] { AdministratorReceivers.ApplyMemberRestoration, AdministratorReceivers.CompleteAdministratorRestoration })]
    [InlineData("assign", new[] { AdministratorReceivers.ApplyAdministratorAssignmentRestoration, AdministratorReceivers.CompleteAdministratorRestoration })]
    [InlineData("neutral-member", new[] { AdministratorReceivers.ApplyNeutralMemberSourceMutation })]
    [InlineData("neutral-assignment", new[] { AdministratorReceivers.ApplyNeutralAdministratorAssignmentMutation })]
    public async Task ShouldRunExactlyOneApplyReceiverGivenTransitionKind(string route, string[] expected)
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());
        var member = await host.AddMemberAsync();
        Uuid operation;
        switch (route)
        {
            case "suspend":
                operation = (await host.SuspendAsync(first, second)).Value!.OperationId;
                break;
            case "remove":
                operation = (await host.RemoveAsync(first, second)).Value!.OperationId;
                break;
            case "reinstate":
                await Complete(host, await host.SuspendAsync(first, second));
                operation = (await host.ReinstateAsync(first, second)).Value!.OperationId;
                break;
            case "assign":
                await Complete(host, await host.RemoveAsync(first, second));
                operation = (await host.AssignAsync(first, second)).Value!.OperationId;
                break;
            case "neutral-member":
                operation = (await host.SuspendAsync(first, member)).Value!.OperationId;
                break;
            default:
                var pinned = await host.AddMemberAsync();
                await Complete(host, await host.SuspendAsync(first, pinned));
                var team = host.AdministratorsTeamId;
                await host.SeedAsync(new TeamMember(host.TenantId, team, member.MemberId),
                    assignment => assignment.Assign(member.EpisodeId));
                operation = (await host.RemoveAsync(first, member)).Value!.OperationId;
                break;
        }

        // Act
        var record = await host.DriveAsync(operation);

        // Assert
        Assert.Equal(ProcessState.Completed, record.State);
        Assert.Equal(expected.Order(StringComparer.Ordinal), record.Facts.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ShouldReturnReplayedGivenRepeatedReceiverCommand()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());
        var operation = (await host.SuspendAsync(first, second)).Value!.OperationId;
        var record = await host.PrepareProcessAsync(operation);
        var apply = await host.CommandAsync(record, AdministratorReceivers.ApplyMemberWithdrawal);

        // Act
        var applied = await host.SendAsync(apply);
        var replayed = await host.SendAsync(apply);
        var completed = await host.DriveAsync(operation);
        var complete = await host.CommandAsync(completed, AdministratorReceivers.CompleteAdministratorWithdrawal);
        var completionReplay = await host.SendAsync(complete);

        // Assert
        var fact = Assert.IsType<ReceiverOutcome.Applied>(applied.Value).Fact;
        Assert.Equal(fact, Assert.IsType<ReceiverOutcome.Replayed>(replayed.Value).Fact);
        Assert.Equal(ProcessState.Completed, completed.State);
        Assert.IsType<ReceiverOutcome.Replayed>(completionReplay.Value);
        Assert.Single((await host.FactsAsync((await host.MemberAsync(second)).Stream)).OfType<MemberSuspended>());
        Assert.Single((await host.GuardFactsAsync()).OfType<TenantManagerWithdrawalCompleted>());
    }

    [Fact]
    public async Task ShouldNotDuplicateGivenReceiverAppendBeforeCheckpointCrash()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());
        var operation = (await host.SuspendAsync(first, second)).Value!.OperationId;
        var record = await host.PrepareProcessAsync(operation);
        Assert.IsType<ReceiverOutcome.Applied>((await host.SendAsync(
            await host.CommandAsync(record, AdministratorReceivers.ApplyMemberWithdrawal))).Value);

        // Act
        var resumed = await host.ResumeAsync(operation);

        // Assert
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Single((await host.FactsAsync((await host.MemberAsync(second)).Stream)).OfType<MemberSuspended>());
        Assert.Equal(2, resumed.Facts.Count);
    }

    [Fact]
    public async Task ShouldQuarantineGivenMemberChangedAfterAdmission()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var first = await host.AddAdministratorAsync();
        await host.AddAdministratorAsync();
        var member = await host.AddMemberAsync();
        var operation = (await host.SuspendAsync(first, member)).Value!.OperationId;
        await host.SeedAsync(new Member(host.TenantId, member.UserId), source =>
            source.Suspend(first.MemberId, "Alex Admin", AdministratorTransitionHost.At, "Raced past the guard."));

        // Act
        var record = await host.DriveAsync(operation);

        // Assert
        Assert.Equal(ProcessState.Quarantined, record.State);
        Assert.Contains(AdministratorReceivers.ApplyNeutralMemberSourceMutation, record.Note, StringComparison.Ordinal);
        Assert.Single((await host.FactsAsync((await host.MemberAsync(member)).Stream)).OfType<MemberSuspended>());
    }

    [Fact]
    public async Task ShouldConflictGivenChangedIntentForSameEffectKey()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());
        var operation = (await host.SuspendAsync(first, second)).Value!.OperationId;
        var record = await host.PrepareProcessAsync(operation);
        var apply = (ApplyMemberWithdrawal)await host.CommandAsync(record, AdministratorReceivers.ApplyMemberWithdrawal);

        // Act
        var tampered = await host.SendAsync(apply with { Intent = apply.Intent with { Sha256 = new string('0', 64) } });
        var wrongKind = await host.SendAsync(apply with { WithdrawalKind = "deprovision_member" });
        var wrongOperation = await host.SendAsync(apply with { OperationId = Uuid.CreateVersion4() });

        // Assert
        foreach (var result in new[] { tampered, wrongKind, wrongOperation })
        {
            Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
            Assert.False(result.Error.IsTransient);
        }

        Assert.False((await host.MemberAsync(second)).IsSuspended);
    }

    [Fact]
    public async Task ShouldRejectGivenInvalidSourceProof()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());
        var operation = (await host.SuspendAsync(first, second)).Value!.OperationId;
        var record = await host.PrepareProcessAsync(operation);
        var apply = (ApplyMemberWithdrawal)await host.CommandAsync(record, AdministratorReceivers.ApplyMemberWithdrawal);

        // Act
        var forged = await host.SendAsync(apply with { Source = apply.Source with { PayloadSha256 = new string('f', 64) } });
        var elsewhere = await host.SendAsync(apply with { Source = apply.Source with { PhysicalPosition = 7 } });

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, forged.Error!.Kind);
        Assert.False(forged.Error.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, elsewhere.Error!.Kind);
        Assert.False(elsewhere.Error.IsTransient);
        Assert.False((await host.MemberAsync(second)).IsSuspended);
    }

    [Fact]
    public async Task ShouldRefuseGivenNonSystemPrincipal()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());
        var operation = (await host.SuspendAsync(first, second)).Value!.OperationId;
        var apply = await host.CommandAsync(await host.PrepareProcessAsync(operation),
            AdministratorReceivers.ApplyMemberWithdrawal);

        // Act
        var refused = await host.SendAsync(apply, AdministratorTransitionHost.Actor(first));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, refused.Error!.Kind);
        Assert.False((await host.MemberAsync(second)).IsSuspended);
    }

    [Fact]
    public async Task ShouldRejectTransientlyGivenUnfinishedChangeToSameMember()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());
        var third = await host.AddAdministratorAsync();
        var suspension = await host.SuspendAsync(first, second);

        // Act
        var deprovision = await host.DeprovisionAsync(first, second);
        await host.DriveAsync(suspension.Value!.OperationId);
        var afterwards = await host.DeprovisionAsync(third, second);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, deprovision.Error!.Kind);
        Assert.True(deprovision.Error.IsTransient);
        Assert.True(afterwards.IsSuccess);
        Assert.Equal(2, (await host.GuardFactsAsync()).OfType<TenantManagerWithdrawalAccepted>().Count());
    }

    [Fact]
    public async Task ShouldStartProcessOnlyForOwnKindGivenGuardFactReactor()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var (first, second) = (await host.AddAdministratorAsync(), await host.AddAdministratorAsync());
        var operation = (await host.SuspendAsync(first, second)).Value!.OperationId;
        var record = await host.GuardRecordAsync(operation);
        await using var scope = host.Provider.CreateAsyncScope();
        var reaction = scope.ServiceProvider.GetRequiredService<ProcessRootReaction>();
        var context = AdministratorTransitionHost.ReactionOf<TenantManagerWithdrawalAccepted>(record);

        // Act
        await new AdministratorAssignmentWithdrawalReactor(new InMemoryProjectionCheckpointStore(), reaction,
            host.Content).HandleAsync(context, CancellationToken.None);
        var notStarted = await host.ProcessAsync(operation);
        await new AdministratorWithdrawalReactor(new InMemoryProjectionCheckpointStore(), reaction, host.Content)
            .HandleAsync(context, CancellationToken.None);
        var started = await host.ProcessAsync(operation);

        // Assert
        Assert.Null(notStarted);
        Assert.NotNull(started);
        Assert.Equal(ProcessState.Completed, started.State);
        Assert.True((await host.MemberAsync(second)).IsSuspended);
    }

    static async Task Complete(AdministratorTransitionHost host, Result<AdministratorTransitionResponse> admitted)
    {
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.Equal(ProcessState.Completed, (await host.DriveAsync(admitted.Value!.OperationId)).State);
    }
}
