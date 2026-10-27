using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Providers.Models;

/// <summary>An authored historical fact, not an approved ProviderVersion.</summary>
public sealed record ProviderView(Uuid TenantId, Uuid ProviderId, long Revision,
    ProviderContent Content, string SourceKind, string Lifecycle,
    IReadOnlyList<string> Unresolved, ActorReference RecordedBy, DateTimeOffset RecordedAt);
