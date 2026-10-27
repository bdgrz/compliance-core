using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.NativeOperations.Services;

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
