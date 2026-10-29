using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.NamedRecords.Models;

/// <summary>
///     The durable intent frozen to the content store when a provider, location or operational process record
///     accepts a change. <paramref name="OldClaimKey" /> is the claim the record holds before the change and
///     <paramref name="NewClaimKey" /> the claim it needs afterwards; both are known before any claim is acquired, and
///     a key is null when the record holds (or will hold) no name claim. <paramref name="DesiredLifecycle" /> is null
///     for providers, which have no lifecycle field. <paramref name="AuthoredContent" /> is the complete normalized
///     content of the change.
/// </summary>
public sealed record RecordNameChangeIntent(DurableIntentHeader Header, NamedRecordKind RecordKind, Uuid RecordId,
    ulong ExpectedRecordRevision, EventStreamAddress? OldClaimKey, EventStreamAddress? NewClaimKey,
    string? DesiredLifecycle, ImmutableContentRef AuthoredContent, string CanonicalNormalizationVersion)
{
    /// <summary>Schema of the frozen intent content.</summary>
    public const string Schema = "record-name-change-intent.v1";

    /// <summary>Version of the name normalization the claim keys were derived with.</summary>
    public const string NormalizationVersion = "v1";
}
