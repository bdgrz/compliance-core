using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Finds a native reference on its exact source stream before building a proof at the actual physical offset.</summary>
static class IdentityRecoverySourceFacts
{
    public static async Task<(SourceFactProof<TFact> Proof, TFact Fact, SourceFactLocator Locator)> BuildAsync<TFact>(
        ISourceFactResolver resolver, IImmutableContentStore content, IDomainEventReader events, string realm,
        NativeFactReference reference, CancellationToken ct) where TFact : DomainEvent
    {
        if (reference.Stream.Realm != realm || reference.EventId == Uuid.Empty || reference.AggregateVersion == 0 ||
            !Uuid.TryParse(reference.Stream.Resource, CultureInfo.InvariantCulture, out var ownerId))
            throw new SourceFactVerificationException("The recovery source reference has another realm or no native identity.");
        ulong position = 0;
        ulong version = 0;
        var ids = new HashSet<Uuid>();
        await foreach (var record in events.ReadAsync(reference.Stream, 0, ct).ConfigureAwait(false))
        {
            var fact = record.Event;
            if (record.Stream != reference.Stream || record.ResourceOffset != position++ ||
                fact.Metadata.EventId == Uuid.Empty || fact.Metadata.AggregateId != ownerId || !ids.Add(fact.Metadata.EventId))
                throw new SourceFactVerificationException("The recovery source has another identity or physical prefix.");
            if (fact.Metadata.IsAudit)
            {
                if (fact.Metadata.EventId == reference.EventId)
                    throw new SourceFactVerificationException("An audit record cannot be the native recovery source.");
                continue;
            }
            if (fact.Metadata.AggregateVersion != ++version)
                throw new SourceFactVerificationException("The recovery source has another native version prefix.");
            if (fact.Metadata.EventId == reference.EventId)
            {
                if (fact is not TFact || fact.Metadata.AggregateVersion != reference.AggregateVersion)
                    throw new SourceFactVerificationException("The recovery reference names another exact native event tuple.");
                return await SourceFactProofs.BuildAsync<TFact>(resolver, content, realm, reference, record.ResourceOffset, ct)
                    .ConfigureAwait(false);
            }
            if (version >= reference.AggregateVersion)
                throw new SourceFactVerificationException("The recovery reference differs from the event at its native version.");
        }
        throw new SourceFactUnavailableException("The exact recovery source native event is not visible yet.");
    }
}
