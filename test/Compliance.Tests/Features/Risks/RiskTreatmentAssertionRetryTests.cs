using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Handlers;
using Bdgrz.Compliance.Features.Risks.Readers;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskTreatmentAssertionRetryTests
{
    [Fact]
    public async Task ShouldReplayOriginalProposalGivenMutableRiskSourceUnavailable()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var inner = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var reader = new UnavailableRiskReader(inner);
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var handler = new ProposeRiskControlTreatmentHandler(new NativeOperationExecutor(reader,
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), content), reader,
            scope.ServiceProvider.GetRequiredService<RiskGovernanceReader>(), scope.ServiceProvider.GetRequiredService<ControlActivationSource>(),
            content, TimeProvider.System);
        var request = new ProposeRiskControlTreatment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.RiskId,
            0, fixture.ControlId, fixture.ControlVersionId, "The exact control addresses the frozen risk.");
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", fixture.AssessorUserId.ToString()),
            new Claim("email", "assertion@example.test")], "BdgrzSession"));
        var first = await handler.HandleAsync(new RequestContext<ProposeRiskControlTreatment>(request, actor), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        reader.Unavailable = true;

        // Act
        var replay = await handler.HandleAsync(new RequestContext<ProposeRiskControlTreatment>(request, actor), CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(0, reader.UnavailableReads);
    }

    [Fact]
    public async Task ShouldRefuseScopedTreatmentLookupGivenSameTreatmentIdentityAcrossTwoControls()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var treatment = Uuid.CreateVersion4();
        var member = RbacIds.Member(fixture.TenantId, fixture.AssessorUserId);
        var actor = ActorReference.ForMember(member, "Assessor");
        var at = DateTimeOffset.UtcNow;
        await RiskSeeds.ProposeTreatmentAsync(provider, fixture.TenantId, fixture.ProgramId, fixture.RiskId, treatment,
            fixture.ControlId, fixture.ControlVersionId, "First exact source.", member, actor, at);
        await RiskSeeds.ProposeTreatmentAsync(provider, fixture.TenantId, fixture.ProgramId, fixture.RiskId, treatment,
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), "Second independent owner.", member, actor, at);
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<RiskGovernanceReader>();

        // Act
        var failure = await Assert.ThrowsAsync<ImmutableContentIntegrityException>(() => reader.LocateControlAsync(fixture.TenantId,
            fixture.ProgramId, fixture.RiskId, treatment).AsTask());

        // Assert
        Assert.Contains("ambiguous", failure.Message, StringComparison.Ordinal);
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
