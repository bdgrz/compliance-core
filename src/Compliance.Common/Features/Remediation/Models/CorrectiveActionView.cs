using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Remediation.Models;

/// <summary>
///     Owned corrective work for a finding; status is open or completed. <paramref name="Revision" /> is the
///     revision of the action's own owner: zero while the admitted action is not materialized, one when open and
///     two when completed. It is the expected revision of <c>CompleteCorrectiveAction</c>.
/// </summary>
public sealed record CorrectiveActionView(Uuid ActionId, string Description, Uuid OwnerMemberId,
    DateOnly DueOn, string Status, ActorReference AddedBy, DateTimeOffset AddedAt,
    string? ResolutionNotes = null, IReadOnlyList<EvidenceReference>? Evidence = null,
    Uuid? CompletedByMemberId = null, ActorReference? CompletedBy = null,
    DateTimeOffset? CompletedAt = null, long Revision = 0);
