using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>Builds the server-created proof of a committed native fact from the broker's own record of it.</summary>
public static class SourceFactProofs
{
    /// <summary>Schema of the content that holds the exact facts a proof requires.</summary>
    public const string RequiredFactsSchema = "source-fact-locator.v1";

    /// <summary>
    ///     Reads the committed fact at the position its reference implies, checks it is the named event of the named
    ///     type, and returns the proof together with the typed fact. The required-facts content is written under a
    ///     content id derived from the event, so building the proof twice yields the same reference.
    /// </summary>
    /// <exception cref="SourceFactVerificationException">The fact is missing, of another type or another event.</exception>
    public static async Task<(SourceFactProof<TEvent> Proof, TEvent Fact, SourceFactLocator Locator)> BuildAsync<TEvent>(
        ISourceFactResolver resolver, IImmutableContentStore content, string realm, NativeFactReference reference,
        CancellationToken ct = default) where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(reference);
        if (reference.AggregateVersion == 0)
            throw new SourceFactVerificationException("A native fact reference names an aggregate version of at least one.");
        var resolved = await resolver.ResolveAsync(reference.Stream, reference.AggregateVersion - 1, ct)
            .ConfigureAwait(false);
        if (resolved.Event is not TEvent typed)
            throw new SourceFactVerificationException($"The record at {reference.Stream} is not a {typeof(TEvent).Name}.");
        var locator = resolved.Locator;
        if (locator.EventId != reference.EventId)
            throw new SourceFactVerificationException("The record is not the referenced event.");
        var required = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(locator.EventId, RequiredFactsSchema),
            RequiredFactsSchema, locator, ct).ConfigureAwait(false);
        var proof = new SourceFactProof<TEvent>(locator.Source, locator.EventId, locator.ResourcePosition,
            locator.AggregateVersion, EventKind.Of(resolved.Event), locator.PayloadSha256, required,
            locator.GlobalPosition, locator.RealmPosition);
        return (proof, typed, locator);
    }
}
