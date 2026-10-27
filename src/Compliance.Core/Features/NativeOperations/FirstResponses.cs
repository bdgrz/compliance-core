using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>Freezes and replays the first response of a root native admission as verified immutable content.</summary>
public static class FirstResponses
{
    /// <summary>Schema of the frozen descriptor.</summary>
    public const string Schema = "first-response-descriptor.v1";

    /// <summary>
    /// Writes the descriptor before the accepting append. Each candidate gets its own content id: two equal intents
    /// racing may freeze different candidates, and only the winner's reference survives on the native fact.
    /// </summary>
    public static Task<ImmutableContentRef> FreezeAsync(IImmutableContentStore store, string realm,
        FirstResponseDescriptor descriptor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(descriptor);
        return store.WriteJsonAsync(realm, Uuid.CreateVersion4(), Schema, descriptor, ct);
    }

    /// <summary>Reads the exact frozen descriptor; any schema, digest or content mismatch fails closed.</summary>
    public static Task<FirstResponseDescriptor> ReplayAsync(IImmutableContentStore store, string realm,
        ImmutableContentRef reference, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(reference);
        if (!string.Equals(reference.Schema, Schema, StringComparison.Ordinal))
            throw new ImmutableContentIntegrityException("The referenced content is not a first response descriptor.");
        return store.ReadJsonAsync<FirstResponseDescriptor>(realm, reference, ct);
    }
}
