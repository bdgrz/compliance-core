using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaLabelRefusalVerifierTests
{
    [Fact]
    public async Task ShouldProveExactRefusalGivenOtherHolderWasLiveAtDecision()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync(false);
        var verifier = new CriteriaLabelRefusalVerifier(fixture.Store, new CriteriaLabelSourceVerifier(fixture.Store));

        // Act
        var proof = await verifier.VerifyAsync("edition_1", fixture.Refusal.Metadata.EventId, CancellationToken.None);

        // Assert
        Assert.NotNull(proof);
        Assert.Equal(fixture.Request.Metadata.EventId, proof.Request.Metadata.EventId);
        Assert.Equal(fixture.Holder.Metadata.EventId, proof.Refusal.HolderReservationEventId);
    }

    [Fact]
    public async Task ShouldRejectRefusalGivenCompetingHolderWasReleasedFirst()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync(true);
        var verifier = new CriteriaLabelRefusalVerifier(fixture.Store, new CriteriaLabelSourceVerifier(fixture.Store));

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            await verifier.VerifyAsync("edition_1", fixture.Refusal.Metadata.EventId, CancellationToken.None));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public async Task ShouldReturnUnobservedGivenRefusalIsMissing()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync(false);
        var verifier = new CriteriaLabelRefusalVerifier(fixture.Store, new CriteriaLabelSourceVerifier(fixture.Store));

        // Act
        var proof = await verifier.VerifyAsync("edition_1", Uuid.CreateVersion4(), CancellationToken.None);

        // Assert
        Assert.Null(proof);
    }

    [Fact]
    public async Task ShouldRejectRefusalGivenSameRequestWasAlreadyFenced()
    {
        // Arrange
        var edition = new CriteriaEdition(Uuid.CreateVersion4());
        var operation = Uuid.CreateVersion4();
        var actor = new ActorAttribution("operator", "bdgrz");
        var request = new CriteriaEditionDraftRequested(edition.Id, operation, operation, 1, "tsc", "edition_1", actor, null, null, null);
        request.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), edition.Id, 1, DateTimeOffset.UtcNow));
        var claim = new EditionLabelClaim("edition_1");
        var holder = new EditionLabelReserved("edition_1", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(), actor);
        holder.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), claim.Id, 1, DateTimeOffset.UtcNow));
        var fence = new EditionLabelReservationFenced("edition_1", edition.Id, operation, request.Metadata.EventId,
            Uuid.CreateVersion4(), "cancelled");
        fence.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), claim.Id, 2, DateTimeOffset.UtcNow));
        var refusal = new EditionLabelReservationRefused("edition_1", edition.Id, operation, 2, request.Metadata.EventId,
            actor, holder.EditionId, holder.OperationId, 1, holder.Metadata.EventId);
        refusal.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), claim.Id, 3, DateTimeOffset.UtcNow));
        var store = new InMemoryEventStore();
        await store.AppendAsync(edition.Stream, 0, [request]);
        await store.AppendAsync(claim.Stream, 0, [holder, fence, refusal]);
        var verifier = new CriteriaLabelRefusalVerifier(store, new CriteriaLabelSourceVerifier(store));

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            await verifier.VerifyAsync("edition_1", refusal.Metadata.EventId, CancellationToken.None));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    sealed record Fixture(InMemoryEventStore Store, CriteriaEditionDraftRequested Request,
        EditionLabelReserved Holder, EditionLabelReservationRefused Refusal)
    {
        public static async Task<Fixture> CreateAsync(bool releaseFirst)
        {
            var store = new InMemoryEventStore();
            var edition = new CriteriaEdition(Uuid.CreateVersion4());
            var operation = Uuid.CreateVersion4();
            var actor = new ActorAttribution("operator", "bdgrz");
            var request = new CriteriaEditionDraftRequested(edition.Id, operation, operation, 1, "tsc", "edition_1", actor, null, null, null);
            request.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), edition.Id, 1, DateTimeOffset.UtcNow));
            await store.AppendAsync(edition.Stream, 0, [request]);
            var claim = new EditionLabelClaim("edition_1");
            var holder = new EditionLabelReserved("edition_1", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1,
                Uuid.CreateVersion4(), actor);
            holder.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), claim.Id, 1, DateTimeOffset.UtcNow));
            var refusal = new EditionLabelReservationRefused("edition_1", edition.Id, operation, 2, request.Metadata.EventId,
                actor, holder.EditionId, holder.OperationId, holder.Generation, holder.Metadata.EventId);
            refusal.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), claim.Id, releaseFirst ? 3UL : 2UL, DateTimeOffset.UtcNow));
            if (releaseFirst)
            {
                var released = new EditionLabelReleased("edition_1", holder.EditionId, holder.OperationId, 1,
                    Uuid.CreateVersion4(), "cancelled", Uuid.Empty);
                released.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), claim.Id, 2, DateTimeOffset.UtcNow));
                await store.AppendAsync(claim.Stream, 0, [holder, released, refusal]);
            }
            else
            {
                await store.AppendAsync(claim.Stream, 0, [holder, refusal]);
            }
            return new Fixture(store, request, holder, refusal);
        }
    }
}
