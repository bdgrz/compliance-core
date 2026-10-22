using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

[Discriminator("bdgrz.finding.corrective_action.completed", 2)]
public sealed record CorrectiveActionCompletedV2(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, Uuid ActionId, string ResolutionNotes, IReadOnlyList<EvidenceReference> Evidence,
    Uuid CompletedByMemberId, ActorReference CompletedBy, DateTimeOffset CompletedAt)
    : DomainEvent
{
    readonly IReadOnlyList<EvidenceReference> _evidence = Array.AsReadOnly(Evidence.ToArray());
    public IReadOnlyList<EvidenceReference> Evidence
    {
        get => _evidence;
        init => _evidence = Array.AsReadOnly(value.ToArray());
    }
}
