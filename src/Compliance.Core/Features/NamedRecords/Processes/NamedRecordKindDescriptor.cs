using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.NamedRecords.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
namespace Bdgrz.Compliance.Features.NamedRecords.Processes;

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
