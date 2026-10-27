using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
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
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Reactors;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

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
