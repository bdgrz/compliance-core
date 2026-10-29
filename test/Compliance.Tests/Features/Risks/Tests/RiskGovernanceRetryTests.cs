using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Remediation.Services;
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
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Handlers;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskGovernanceRetryTests
{
    [Fact]
    public async Task ShouldReplayOriginalAssignmentGivenRiskSourceCannotBeReselected()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("accept");
        await using var provider = fixture.Provider;
        var person = await fixture.SeedPersonAsync(fixture.ApproverUserId);
        await using var scope = provider.CreateAsyncScope();
        var inner = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var reader = new UnavailableRiskReader(inner);
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var handler = new AssignRiskOwnerHandler(new NativeOperationExecutor(reader, executor, content), reader,
            content, TimeProvider.System);
        var request = new AssignRiskOwner(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.RiskId,
            0, person, "Owns the recorded risk.");
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"),
            new Claim("sub", fixture.AssessorUserId.ToString()), new Claim("email", "owner@example.test")], "BdgrzSession"));
        var first = await handler.HandleAsync(new RequestContext<AssignRiskOwner>(request, actor), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        reader.Unavailable = true;

        // Act
        var replay = await handler.HandleAsync(new RequestContext<AssignRiskOwner>(request, actor), CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(0, reader.UnavailableReads);
        Assert.Equal(1, (await inner.HydrateAsync(new RiskGovernance(fixture.TenantId, fixture.ProgramId, fixture.RiskId))).Revision);
    }

    [Fact]
    public async Task ShouldRaiseOneSemanticTriggerGivenDifferentOrphanContentBeforeAdmission()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("accept");
        await using var provider = fixture.Provider;
        var at = DateTimeOffset.UtcNow;
        var source = Uuid.CreateVersion4().ToString();
        var trigger = RiskGovernance.TriggerIdFor(fixture.ProgramId, fixture.RiskId, "method_changed", source);
        var command = new RaiseRiskReassessmentTriggers(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            "method_changed", source, at, Uuid.CreateVersion4(), fixture.RiskId);
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        await FindingContent.WriteDeterministicAsync(content, fixture.TenantId, trigger, RiskContent.TriggerSchema,
            new RiskReassessmentTriggerView(trigger, fixture.RiskId, "method_changed", source, at.AddMinutes(-1)), CancellationToken.None);

        // Act
        await RequestScenario.For(provider).GivenActor(RequestActor.System).When(command).ExpectSuccess();
        await RequestScenario.For(provider).GivenActor(RequestActor.System)
            .When(command with { CommandId = Uuid.CreateVersion4(), RaisedAt = at.AddMinutes(2) }).ExpectSuccess();
        var snapshot = await scope.ServiceProvider.GetRequiredService<RiskGovernanceReader>()
            .ReadRiskAsync(fixture.TenantId, fixture.ProgramId, fixture.RiskId);

        // Assert
        var captured = Assert.Single(snapshot.OpenTriggers(fixture.RiskId, []));
        Assert.Equal(trigger, captured.TriggerId);
        Assert.Equal(at, captured.RaisedAt);
        Assert.Equal(1, snapshot.RevisionOf(fixture.RiskId));
    }
    sealed class UnavailableRiskReader(IAggregateReader inner) : IAggregateReader
    {
        public bool Unavailable { get; set; }
        public int UnavailableReads { get; private set; }
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (Unavailable && aggregate is RiskDraft)
            {
                UnavailableReads++;
                return ValueTask.FromResult(aggregate);
            }
            return inner.HydrateAsync(aggregate, ct);
        }
    }

}
