using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Reactors;
using Bdgrz.Compliance.Features.Risks.Readers;
using System.Security.Claims;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskMethodReassessmentNativeTests
{
    [Fact]
    public async Task ShouldSkipReassessmentGivenVerifiedFirstMethodPublication()
    {
        // Arrange
        await using var fixture = await RiskMethodPublicationTests.Fixture.CreateAsync();
        Assert.True((await fixture.Handler.HandleAsync(fixture.Context(fixture.Request(0, 12)), CancellationToken.None)).IsSuccess);
        var owner = await fixture.Reader.HydrateAsync(new RiskMethod(fixture.Tenant, fixture.Program));
        var reactor = await ReactorAsync(fixture);

        // Act
        await reactor.HandleAsync(new Context(owner.Latest!, owner.Stream), CancellationToken.None);

        // Assert
        Assert.Equal(1, owner.PublishedVersion);
        Assert.Equal(EventStreamPattern.ForPattern(fixture.Tenant.ToString(), RiskMethod.Area), reactor.Pattern);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRefuseForgedTriggerGivenSamePublicationEventIdentity(bool wrongBody)
    {
        // Arrange
        await using var fixture = await RiskMethodPublicationTests.Fixture.CreateAsync();
        Assert.True((await fixture.Handler.HandleAsync(fixture.Context(fixture.Request(0, 12)), CancellationToken.None)).IsSuccess);
        var owner = await fixture.Reader.HydrateAsync(new RiskMethod(fixture.Tenant, fixture.Program));
        var original = owner.Latest!;
        var forged = original with { PublishedVersion = wrongBody ? 2 : original.PublishedVersion };
        forged.AttachMetadata(wrongBody ? original.Metadata : original.Metadata with { OccurredOn = original.Metadata.OccurredOn.AddHours(1) });
        var reactor = await ReactorAsync(fixture);

        // Act
        var failure = await Assert.ThrowsAsync<ImmutableContentIntegrityException>(() =>
            reactor.HandleAsync(new Context(forged, owner.Stream), CancellationToken.None).AsTask());

        // Assert
        Assert.NotEmpty(failure.Message);
        Assert.Equal(original.Metadata.EventId, (await fixture.Reader.HydrateAsync(new RiskMethod(fixture.Tenant, fixture.Program))).Latest!.Metadata.EventId);
    }

    static async Task<RiskMethodReassessmentReactor> ReactorAsync(RiskMethodPublicationTests.Fixture fixture)
    {
        var directory = new EmptyRisks();
        var reactor = new RiskMethodReassessmentReactor(new InMemoryProjectionCheckpointStore(), fixture.Bus, fixture.Reader,
            directory, new RiskDraftListReadConsistency(directory, fixture.Events),
            new RiskMethodReader(fixture.Reader, fixture.Events, fixture.Content));
        await new ReactorScenario(new TenantId(fixture.Tenant.ToString())).RunAsync(reactor);
        return reactor;
    }

    sealed class Context(RiskMethodVersionPublished trigger, EventStreamAddress stream) : IReactorContext<RiskMethodVersionPublished>
    {
        public RiskMethodVersionPublished Trigger { get; } = trigger;
        public DomainEventRecord Source { get; } = new(stream, trigger, 0, EventCursor.Start);
        public ClaimsPrincipal Actor { get; } = RequestActor.System;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid CauseId => Trigger.Metadata.EventId;
        public DateTimeOffset StartedAt => Trigger.Operation.AcceptedAt;
    }

    sealed class EmptyRisks : IRiskDraftDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenant, CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
        public ValueTask<RiskDraftView?> GetAsync(Uuid tenant, Uuid risk, CancellationToken ct = default) => ValueTask.FromResult<RiskDraftView?>(null);
        public ValueTask<Page<RiskDraftView>> ListProgramAsync(Uuid tenant, Uuid program, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<RiskDraftView>([], null));
        public ValueTask<RiskDraftRevisionView?> GetRevisionAsync(Uuid tenant, Uuid risk, long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<RiskDraftRevisionView?>(null);
    }
}
