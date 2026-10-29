using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class TenantManagerNativeHistoryTests
{
    [Theory]
    [InlineData("payload")]
    [InlineData("event_id")]
    [InlineData("truncate")]
    [InlineData("correlation")]
    [InlineData("causation")]
    [InlineData("execution")]
    [InlineData("actor")]
    public async Task ShouldRefuseDifferentUnselectedIntermediatePrefixGivenCapturedGuardAuthority(string fault)
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var first = await host.AddAdministratorAsync();
        var second = await host.AddAdministratorAsync();
        var suspended = await host.SuspendAsync(first, second);
        Assert.True(suspended.IsSuccess, suspended.Error?.Message);
        Assert.Equal(ProcessState.Completed, (await host.DriveAsync(suspended.Value!.OperationId)).State);
        var reinstated = await host.ReinstateAsync(first, second);
        Assert.True(reinstated.IsSuccess, reinstated.Error?.Message);
        Assert.Equal(ProcessState.Completed, (await host.DriveAsync(reinstated.Value!.OperationId)).State);
        var owner = await host.GuardAsync();
        var baseline = await host.Content.ReadJsonAsync<GuardBaseline>(host.TenantId.ToString(), owner.BaselineRef!);
        var facts = (await host.GuardFactsAsync()).ToArray();
        if (fault == "truncate")
            facts = facts[..^1];
        else
        {
            var index = Array.FindIndex(facts, value => value is TenantManagerWithdrawalCompleted);
            var original = (TenantManagerWithdrawalCompleted)facts[index];
            var altered = fault == "payload" ? original with { SourceProofs = original.SourceProofs! with { ContentId = Uuid.CreateVersion4() } } : original with { };
            altered.AttachMetadata(fault switch
            {
                "event_id" => original.Metadata with { EventId = Uuid.CreateVersion4() },
                "correlation" => original.Metadata with { CorrelationId = Uuid.CreateVersion4() },
                "causation" => original.Metadata with { CausationId = Uuid.CreateVersion4() },
                "execution" => original.Metadata with { ExecutionId = Uuid.CreateVersion4() },
                "actor" => original.Metadata with { Actor = new ActorAttribution("another-worker", "bdgrz.system") },
                _ => original.Metadata,
            });
            facts[index] = altered;
        }

        // Act
        var failure = await Record.ExceptionAsync(async () => _ = await TenantManagerGuardHistory.ReadAsync(new Records(owner.Stream, facts), owner, baseline));

        // Assert
        if (fault == "truncate")
            Assert.IsType<ImmutableContentUnavailableException>(failure);
        else
            Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(2, owner.AvailableCount);
        Assert.Equal(0, owner.PendingCount);
    }

    [Fact]
    public async Task ShouldReconstructNeutralAndUnavailableEpisodeGivenDefaultAuthorityCompactedHistory()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var actor = await host.AddAdministratorAsync();
        var target = await host.AddMemberAsync();
        var suspended = await host.SuspendAsync(actor, target);
        Assert.True(suspended.IsSuccess, suspended.Error?.Message);
        Assert.Equal(ProcessState.Completed, (await host.DriveAsync(suspended.Value!.OperationId)).State);
        var assigned = await host.AssignAsync(actor, target);
        Assert.True(assigned.IsSuccess, assigned.Error?.Message);
        Assert.Equal(ProcessState.Completed, (await host.DriveAsync(assigned.Value!.OperationId)).State);

        // Act
        var current = await host.GuardAsync();
        var selected = await host.GuardAsync(target.EpisodeId);
        var baseline = await host.Content.ReadJsonAsync<GuardBaseline>(host.TenantId.ToString(), current.BaselineRef!);
        var complete = await TenantManagerGuardHistory.ReadAsync(host.Events, current, baseline);

        // Assert
        Assert.False(current.HasContribution(target.EpisodeId));
        Assert.True(selected.HasContribution(target.EpisodeId));
        Assert.Contains(TenantManagerGuard.Suspended, selected.ReasonsFor(target.EpisodeId));
        Assert.False(selected.IsAvailable(target.EpisodeId));
        Assert.Equal(1, current.AvailableCount);
        Assert.True(complete.EpisodeOf(target.EpisodeId)!.HasContribution);
        Assert.Contains(TenantManagerGuard.Suspended, complete.EpisodeOf(target.EpisodeId)!.NeutralReasons);
    }

    [Fact]
    public async Task ShouldUseWinningPinnedBaselineGivenCallerCapturedEarlierEmptyStreamCut()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var actor = await host.AddAdministratorAsync();
        var target = await host.AddAdministratorAsync();
        var earlier = await new TenantManagerGuardBootstrap(host.Events, host.Reader, TimeProvider.System)
            .BuildAsync(host.TenantId, CancellationToken.None);
        var third = await host.AddAdministratorAsync();
        var winner = await host.SuspendAsync(actor, target);
        Assert.True(winner.IsSuccess, winner.Error?.Message);
        Assert.Equal(2, earlier.Contributions.Count);

        // Act
        var captured = await host.Reader.HydrateAsync(new TenantManagerGuard(host.TenantId, baseline: earlier));
        var pinned = await host.Content.ReadJsonAsync<GuardBaseline>(host.TenantId.ToString(), captured.BaselineRef!);

        // Assert
        Assert.Equal(3, pinned.Contributions.Count);
        Assert.Equal(2, captured.AvailableCount);
        Assert.True(captured.IsAvailable(actor.EpisodeId));
        Assert.True(captured.IsAvailable(third.EpisodeId));
        Assert.False(captured.IsAvailable(target.EpisodeId));
        Assert.Equal(1, captured.PendingCount);
    }

    sealed class Records(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset, CancellationToken ct = default) => Enumerate(address, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor, CancellationToken ct = default) => throw new NotSupportedException();
        async IAsyncEnumerable<DomainEventRecord> Enumerate(EventStreamAddress address, ulong fromOffset, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            if (address != stream)
                yield break;
            for (var index = fromOffset; index < (ulong)facts.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[index], index, EventCursor.Start);
            }
        }
    }
}
