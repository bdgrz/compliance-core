using System.Security.Claims;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Risks;

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
