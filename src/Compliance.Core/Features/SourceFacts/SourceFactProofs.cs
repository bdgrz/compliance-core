using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
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
    public static Task<(SourceFactProof<TEvent> Proof, TEvent Fact, SourceFactLocator Locator)> BuildAsync<TEvent>(
        ISourceFactResolver resolver, IImmutableContentStore content, string realm, NativeFactReference reference,
        CancellationToken ct = default) where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(reference);
        return BuildAsync<TEvent>(resolver, content, realm, reference,
            reference.AggregateVersion == 0 ? 0 : reference.AggregateVersion - 1, ct);
    }

    /// <summary>Builds from the actual resource position discovered in the exact scoped native source stream, including preceding audits.</summary>
    public static async Task<(SourceFactProof<TEvent> Proof, TEvent Fact, SourceFactLocator Locator)> BuildAsync<TEvent>(
        ISourceFactResolver resolver, IImmutableContentStore content, string realm, NativeFactReference reference,
        ulong resourcePosition, CancellationToken ct = default) where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(reference);
        if (reference.Stream is null || reference.Stream.Realm != realm)
            throw new SourceFactVerificationException("The requested source proof belongs to another exact realm.");
        if (reference.EventId == Uuid.Empty || reference.AggregateVersion == 0 || resourcePosition < reference.AggregateVersion - 1)
            throw new SourceFactVerificationException("A native fact reference requires an event identity, positive version and possible physical source position.");
        var resolved = await resolver.ResolveAsync(reference.Stream, resourcePosition, ct)
            .ConfigureAwait(false);
        if (resolved.Event is not TEvent typed)
            throw new SourceFactVerificationException($"The record at {reference.Stream} is not a {typeof(TEvent).Name}.");
        var locator = resolved.Locator;
        if (locator.Source != reference.Stream || locator.ResourcePosition != resourcePosition ||
            locator.EventId != reference.EventId || locator.AggregateVersion != reference.AggregateVersion ||
            typed.Metadata.IsAudit || typed.Metadata.EventId != reference.EventId ||
            typed.Metadata.AggregateVersion != reference.AggregateVersion ||
            locator.PayloadSha256 != ContentDigest.Sha256(typed, typeof(TEvent)))
            throw new SourceFactVerificationException("The returned source does not match the exact requested native tuple and typed payload.");
        var required = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(locator.EventId, RequiredFactsSchema),
            RequiredFactsSchema, locator, ct).ConfigureAwait(false);
        var proof = new SourceFactProof<TEvent>(locator.Source, locator.EventId, locator.ResourcePosition,
            locator.AggregateVersion, EventKind.Of(resolved.Event), locator.PayloadSha256, required,
            locator.GlobalPosition, locator.RealmPosition);
        return (proof, typed, locator);
    }
}
