using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Workforce.Models;

/// <summary>
///     A frozen workforce roster. <c>ContentSha256</c> identifies the complete frozen content,
///     including restricted fields that this read may have redacted.
/// </summary>
public sealed record WorkforceRosterSnapshotView(Uuid TenantId, Uuid SnapshotId,
    Uuid RootSnapshotId, Uuid? AmendsSnapshotId, string ContentSha256, long RowCount,
    string? AmendmentReason, ActorReference FrozenBy, DateTimeOffset FrozenAt,
    IReadOnlyList<FrozenPerson> People, IReadOnlyList<FrozenWorkRelationship> WorkRelationships,
    bool RestrictedFieldsRedacted);
