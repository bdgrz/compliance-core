using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>An existing read-only program ledger prefix was sealed into immutable per-item history before its first native write.</summary>
[Discriminator("bdgrz.work.assignment-history-imported", 2)]
public sealed record WorkAssignmentLegacyHistoryImported(Uuid TenantId, Uuid ProgramId, Uuid WorkItemId,
    Uuid ImportedByCommandId, long Revision, Uuid? AssigneeMemberId, bool Escalated,
    ImmutableContentRef History) : DomainEvent;
