using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Immutable assertion wording and exact package/source bindings for one version.</summary>
public sealed record ManagementAssertionVersionView(
    int Version,
    ManagementAssertionContent Content,
    string ContentSha256,
    string Status,
    Uuid AuthoredByMemberId,
    string AuthoredByDisplay,
    DateTimeOffset AuthoredAt,
    string ChangeReason);
