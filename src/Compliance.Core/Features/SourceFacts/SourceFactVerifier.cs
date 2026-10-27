using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>Shared fail-closed comparison between a stored record and a proof, used by every resolver.</summary>
static class SourceFactVerifier
{
    public static ResolvedSourceFact Build(EventStreamAddress stream, ulong resourcePosition, ulong realmPosition,
        ulong globalPosition, DomainEvent domainEvent)
    {
        var metadata = domainEvent.Metadata ?? throw new SourceFactVerificationException(
            "The source record carries no event metadata.");
        if (metadata.IsAudit)
            throw new SourceFactVerificationException("An audit record is not a source fact.");
        return new ResolvedSourceFact(domainEvent, new SourceFactLocator(stream, metadata.EventId, resourcePosition,
            realmPosition, globalPosition, metadata.AggregateVersion,
            ContentDigest.Sha256(domainEvent, domainEvent.GetType())));
    }

    public static VerifiedSourceProof<TEvent> Verify<TEvent>(SourceFactProof<TEvent> proof, ResolvedSourceFact? fact)
        where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(proof);
        if (fact is null)
            throw new SourceFactVerificationException("The proof names a record that does not exist.");
        var locator = fact.Locator;
        if (fact.Event is not TEvent typed || proof.EventKind != EventKind.Of(fact.Event))
            throw new SourceFactVerificationException("The stored event kind does not match the proof.");
        if (locator.Source.Realm != proof.Source.Realm)
            throw new SourceFactVerificationException("The stored record belongs to a different realm.");
        if (locator.Source != proof.Source)
            throw new SourceFactVerificationException("The stored record belongs to a different stream.");
        if (locator.EventId != proof.EventId)
            throw new SourceFactVerificationException("The stored event id does not match the proof.");
        if (locator.ResourcePosition != proof.PhysicalPosition || locator.RealmPosition != proof.RealmPosition ||
            locator.GlobalPosition != proof.GlobalPosition)
            throw new SourceFactVerificationException("The stored record positions do not match the proof.");
        if (locator.AggregateVersion != proof.OwnerRevision)
            throw new SourceFactVerificationException("The stored aggregate version does not match the proof.");
        if (!string.Equals(locator.PayloadSha256, proof.PayloadSha256, StringComparison.OrdinalIgnoreCase))
            throw new SourceFactVerificationException("The stored payload digest does not match the proof.");
        return new VerifiedSourceProof<TEvent>(locator.Source, locator.EventId, locator.ResourcePosition,
            locator.AggregateVersion, locator.PayloadSha256, typed, locator.GlobalPosition, locator.RealmPosition);
    }
}
