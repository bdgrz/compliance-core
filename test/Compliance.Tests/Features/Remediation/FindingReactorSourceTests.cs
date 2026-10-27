using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Reactors;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.Readiness;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

public sealed class FindingReactorSourceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRefuseAlteredNativeTriggerBeforeProcessCreationGivenRealCommittedSource(bool closure)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = closure ? await FindingTests.RaiseWithCompletedActionAsync(fixture)
            : await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        Uuid root;
        if (closure)
        {
            var close = fixture.Close(finding);
            root = close.CommandId;
            await PersonalReadinessClosureTransportTests.CloseHttpAsync(provider, fixture.ApproverUserId, close);
        }
        else
        {
            finding = await fixture.AddActionAsync(finding, fixture.OwnerMemberId, fixture.Today.AddDays(7),
                materialize: false);
            root = Assert.Single(finding.CorrectiveActions).ActionId;
        }
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var events = services.GetRequiredService<IDomainEventReader>();
        var stream = new FindingAggregate(fixture.TenantId, fixture.ProgramId, finding.FindingId).Stream;
        DomainEventRecord? source = null;
        await foreach (var record in events.ReadAsync(stream, 0))
            if (closure && record.Event is FindingClosureAccepted || !closure && record.Event is CorrectiveActionAdmitted)
                source = record;
        Assert.NotNull(source);
        var process = services.GetRequiredService<ProcessRootReaction>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var resolver = services.GetRequiredService<ISourceFactResolver>();
        var checkpoints = new InMemoryProjectionCheckpointStore();
        ReactionCommandFailedException error;

        // Act
        if (closure)
        {
            var actual = Assert.IsType<FindingClosureAccepted>(source.Event);
            var changed = actual with { Operation = actual.Operation with { Episode = "forged_episode" } };
            changed.AttachMetadata(actual.Metadata);
            var altered = new DomainEventRecord(stream, changed, source.ResourceOffset, source.NextCursor);
            var reactor = new FindingClosureReactor(checkpoints, process, content, resolver);
            await new ReactorScenario(new TenantId(fixture.TenantId.ToString())).RunAsync(reactor);
            error = await Assert.ThrowsAsync<ReactionCommandFailedException>(async () =>
                await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<FindingClosureAccepted>(altered), CancellationToken.None));
        }
        else
        {
            var actual = Assert.IsType<CorrectiveActionAdmitted>(source.Event);
            var changed = actual with { Operation = actual.Operation with { Episode = "forged_episode" } };
            changed.AttachMetadata(actual.Metadata);
            var altered = new DomainEventRecord(stream, changed, source.ResourceOffset, source.NextCursor);
            var reactor = new CorrectiveActionMaterializationReactor(checkpoints, process, content, resolver);
            await new ReactorScenario(new TenantId(fixture.TenantId.ToString())).RunAsync(reactor);
            error = await Assert.ThrowsAsync<ReactionCommandFailedException>(async () =>
                await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<CorrectiveActionAdmitted>(altered), CancellationToken.None));
        }
        var name = closure ? FindingClosureEffects.ProcessName : FindingClosureEffects.MaterializationProcessName;
        var recordAfter = await services.GetRequiredService<IProcessRecordStore>().ReadAsync(ProcessKey.ForRoot(name,
            root, NativeFactReference.Of(stream, source.Event)));

        // Assert
        Assert.False(error.Error.IsTransient);
        Assert.Null(recordAfter);
    }
}
