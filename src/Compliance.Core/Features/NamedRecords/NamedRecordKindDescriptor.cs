using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NamedRecords;

/// <summary>
///     Everything the shared admission, receivers and process runner need to know about one named record family: its
///     owners, how a content document names a claim, and the wire vocabulary of its commands and schemas. The three
///     families (provider, location, operational process) differ only in these values.
/// </summary>
public sealed record NamedRecordKindDescriptor(
    NamedRecordKind Kind,
    string Noun,
    string ClaimOwnerType,
    string ClaimArea,
    string CreateCommandKind,
    string ReviseCommandKind,
    string ClaimCommandKind,
    string ApplyCommandKind,
    string ReleaseCommandKind,
    string CancelCommandKind,
    string AuthoredContentSchema,
    string CreatedContentSchema,
    string RevisedContentSchema,
    string ProcessName,
    Func<Uuid, Uuid, Uuid, NamedRecord> OpenRecord,
    Func<Uuid, EventStreamAddress, Uuid, NameClaim> OpenClaim,
    Func<ReadOnlyMemory<byte>, NamedContentName> ReadName,
    Func<bool, Uuid, long, (string Schema, byte[] Bytes)> ResultContent)
{
    /// <summary>The claim stream of a normalized name; the name kind only takes part for locations.</summary>
    public EventStreamAddress ClaimStream(Uuid tenantId, string? nameKind, string normalizedName)
    {
        ArgumentException.ThrowIfNullOrEmpty(normalizedName);
        var identity = Kind == NamedRecordKind.Location
            ? AggregateIdentity.Create(ClaimOwnerType, tenantId, nameKind ?? string.Empty, normalizedName)
            : AggregateIdentity.Create(ClaimOwnerType, tenantId, normalizedName);
        return new EventStreamAddress(tenantId.ToString(), ClaimArea, identity.ToString());
    }

    /// <summary>The claim stream a content document needs, or null when the content holds no active name.</summary>
    public EventStreamAddress? ClaimStreamOf(Uuid tenantId, NamedContentName name) =>
        name.NormalizedName is null ? null : ClaimStream(tenantId, name.NameKind, name.NormalizedName);
}

/// <summary>The name a content document claims: its kind where kinds partition names, its normalized form, its lifecycle.</summary>
/// <param name="NameKind">The partition of the name, such as a location kind; a constant for families without one.</param>
/// <param name="NormalizedName">The normalized name, or null when the content is retired and claims nothing.</param>
/// <param name="Lifecycle">The lifecycle word of the content, or null for families without one.</param>
public sealed record NamedContentName(string NameKind, string? NormalizedName, string? Lifecycle);
