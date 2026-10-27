using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
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
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Handlers;
using Bdgrz.Compliance.Features.Risks.Queries;
using Bdgrz.Compliance.Features.Risks.Reactors;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskGovernanceNativeTests
{
    [Fact]
    public async Task ShouldPreserveEverySemanticTriggerGivenBoundedCurrentOwnerAndLaterAssignments()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("accept");
        await using var provider = fixture.Provider;
        var at = DateTimeOffset.UtcNow;
        for (var index = 0; index < 24; index++)
            await RequestScenario.For(provider).GivenActor(RequestActor.System).When(new RaiseRiskReassessmentTriggers(
                Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, "method_changed", Uuid.CreateVersion4().ToString(),
                at.AddMinutes(index), Uuid.CreateVersion4(), fixture.RiskId)).ExpectSuccess();
        var person = await fixture.SeedPersonAsync(fixture.ApproverUserId);
        await fixture.Scenario(fixture.AssessorUserId).When(new AssignRiskOwner(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, fixture.RiskId, 24, person, "Owns every captured trigger.")).ExpectSuccess();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var history = scope.ServiceProvider.GetRequiredService<RiskGovernanceReader>();

        // Act
        var owner = await reader.HydrateAsync(new RiskGovernance(fixture.TenantId, fixture.ProgramId, fixture.RiskId));
        var facts = await history.ReadGovernanceFactsAsync(owner);
        var snapshot = await history.ReadRiskAsync(fixture.TenantId, fixture.ProgramId, fixture.RiskId);

        // Assert
        Assert.Equal(25, owner.Revision);
        Assert.Equal(25, facts.Count);
        Assert.Equal(24, snapshot.OpenTriggers(fixture.RiskId, []).Count);
        Assert.Equal(person, snapshot.OwnerOf(fixture.RiskId)!.PersonId);
        Assert.Equal(facts[^1].Metadata.EventId, owner.Latest!.Metadata.EventId);
        Assert.NotNull(owner.Owner);
    }

    [Fact]
    public async Task ShouldFenceTriggerAdmissionGivenAnotherFactChangedTheVerifiedHistoryCut()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("accept");
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var before = await reader.HydrateAsync(new RiskGovernance(fixture.TenantId, fixture.ProgramId, fixture.RiskId));
        var person = await fixture.SeedPersonAsync(fixture.ApproverUserId);
        await fixture.Scenario(fixture.AssessorUserId).When(new AssignRiskOwner(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, fixture.RiskId, 0, person, "Owner wins the append race.")).ExpectSuccess();
        var current = await reader.HydrateAsync(new RiskGovernance(fixture.TenantId, fixture.ProgramId, fixture.RiskId));
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var reference = Uuid.CreateVersion4().ToString();
        var id = RiskGovernance.TriggerIdFor(fixture.ProgramId, fixture.RiskId, "method_changed", reference);
        var at = DateTimeOffset.UtcNow;
        var operation = RootOperation.Provenance(Uuid.CreateVersion4(), nameof(RaiseRiskReassessmentTriggers), current.Stream,
            ActorReference.ForSystemProcess("reactor:" + RiskMethodReassessmentReactor.WorkloadName,
                RiskMethodReassessmentReactor.WorkloadName), null, new string('a', 64), at) with
        { FirstOwner = current.Stream };
        var blob = await FindingContent.WriteAsync(content, fixture.TenantId, RiskContent.TriggerSchema,
            new RiskReassessmentTriggerView(id, fixture.RiskId, "method_changed", reference, at), CancellationToken.None);
        var response = await RiskCommands.FreezeUnitAsync(content, fixture.TenantId, CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, fixture.TenantId.ToString(), response.Response);

        // Act
        var rejected = current.RaiseTrigger(operation, id, blob, first, response.Result, before.Revision, before.CommittedStreamPosition);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, rejected.Error?.Kind);
        Assert.True(rejected.Error!.IsTransient);
        Assert.Empty(new AggregateScenario<RiskGovernance>(current).PendingEvents);
        Assert.Equal(1, current.Revision);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldClassifyGovernanceReadFailureGivenMissingOrCorruptImmutableOwner(bool unavailable)
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("accept");
        await using var provider = fixture.Provider;
        var person = await fixture.SeedPersonAsync(fixture.ApproverUserId);
        await fixture.Scenario(fixture.AssessorUserId).When(new AssignRiskOwner(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, fixture.RiskId, 0, person, "Owner body will be unavailable.")).ExpectSuccess();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var handler = new GetRiskGovernanceHandler(reader, new RiskGovernanceReader(reader, events,
            new FailedContent(content, unavailable)), TimeProvider.System, scope.ServiceProvider.GetRequiredService<RiskEvaluationReader>());

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetRiskGovernance>(new GetRiskGovernance(fixture.TenantId,
            fixture.ProgramId, fixture.RiskId), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
    }

    sealed class FailedContent(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            throw (unavailable ? new ImmutableContentUnavailableException("The owner is unavailable.")
                : new ImmutableContentIntegrityException("The owner is corrupt."));
    }
}
