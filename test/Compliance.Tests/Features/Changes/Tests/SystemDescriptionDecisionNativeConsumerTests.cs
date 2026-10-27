using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Changes.Reactors;
using Bdgrz.Compliance.Features.Changes.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Events;
using Bdgrz.Compliance.Features.SystemDescription.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Changes.Tests;

public sealed class SystemDescriptionDecisionNativeConsumerTests
{
    [Fact]
    public async Task ShouldAdvanceCheckpointWithoutChildEffectGivenNoChangeNoOpAcceptance()
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        var trigger = await SeedNoOpAsync(fixture);
        var scenario = new ReactorScenario(new TenantId(fixture.Tenant.ToString())).Given(trigger);
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var assessments = new UnusedAssessments();
        var reactor = new SystemDescriptionDecisionReactor(checkpoints, TypedReactionBus.For(scenario), fixture.Reader,
            fixture.Descriptions, fixture.Content, assessments);

        // Act
        await scenario.RunAsync(reactor);
        var after = await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern));

        // Assert
        Assert.Equal(0, trigger.ChangeCount);
        Assert.Empty(TypedReactionBus.Sent(scenario));
        Assert.Equal(0, assessments.Reads);
        Assert.NotEqual(ProjectionCheckpoint.Start, after);
    }

    [Fact]
    public async Task ShouldKeepCheckpointGivenForgedNativeDecisionWithCommittedSourceIdentity()
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        var accepted = await SeedNoOpAsync(fixture);
        var trigger = accepted with { ActorMemberId = Uuid.CreateVersion4() };
        trigger.AttachMetadata(accepted.Metadata);
        var scenario = new ReactorScenario(new TenantId(fixture.Tenant.ToString())).Given(trigger);
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new SystemDescriptionDecisionReactor(checkpoints, TypedReactionBus.For(scenario), fixture.Reader,
            fixture.Descriptions, fixture.Content, new UnusedAssessments());

        // Act
        await Assert.ThrowsAsync<ImmutableContentIntegrityException>(() => scenario.RunAsync(reactor));
        var after = await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern));

        // Assert
        Assert.Empty(TypedReactionBus.Sent(scenario));
        Assert.Equal(ProjectionCheckpoint.Start, after);
    }

    static async Task<SystemDescriptionNoChangeRecorded> SeedNoOpAsync(SystemDescriptionNativeFixture fixture)
    {
        var change = Uuid.CreateVersion4();
        var rules = new SystemDescriptionRules(fixture.Tenant, fixture.Program, fixture.Description);
        var content = SystemDescriptionNativeFixture.Body() with
        {
            PeriodChanges = [new SystemDescriptionPeriodChange(change, false, new string('a', 64))],
        };
        Assert.Null(rules.Create(Uuid.CreateVersion4(), new SystemDescriptionScope("type_ii", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4()), content,
            RbacIds.Member(fixture.Tenant, fixture.Author), "Author", fixture.Clock.Now));
        await DescriptionSeeds.WriteAsync(fixture.Writer, fixture.Content, rules);
        var handler = new RecordSystemDescriptionNoChangeDecisionHandler(fixture.Operations);
        var request = new RecordSystemDescriptionNoChangeDecision(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program,
            fixture.Description, 1, change, "The exact assessed change does not alter this narrative.", 1);
        Assert.True((await handler.HandleAsync(fixture.Context(request, fixture.Reviewer), CancellationToken.None)).IsSuccess);
        var repeated = request with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 2 };
        Assert.True((await handler.HandleAsync(fixture.Context(repeated, fixture.Reviewer), CancellationToken.None)).IsSuccess);
        return Assert.IsType<SystemDescriptionNoChangeRecorded>((await fixture.FactsAsync())[^1]);
    }

    sealed class UnusedAssessments : ISystemDescriptionDecisionCorrelationReader
    {
        public int Reads { get; private set; }
        public ValueTask<ComplianceImpactAssessmentCorrelation?> GetCurrentChangeAsync(Uuid tenantId, Uuid programId, Uuid changeId, CancellationToken ct = default)
        {
            Reads++;
            throw new InvalidOperationException("A no-op or forged description source must not inspect change assessments.");
        }
        public ValueTask<ComplianceImpactAssessmentCorrelation?> GetCurrentIncidentAsync(Uuid tenantId, Uuid programId, Uuid incidentId, CancellationToken ct = default)
        {
            Reads++;
            throw new InvalidOperationException("A no-op or forged description source must not inspect incident assessments.");
        }
    }
}
