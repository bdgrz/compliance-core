using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Events;

/// <summary>
///     A provisional native effect of one frozen row on its target. It advances the target's physical stream, never
///     its published content: the candidate becomes the published application only when its batch commits and the
///     target hold settles. <paramref name="ResultingRevision" /> is the candidate content revision after this row;
///     the target group's candidate revision is that of its last row.
/// </summary>
[Discriminator("bdgrz.application_import.effect_pending", 2)]
public sealed record ApplicationImportEffectPending(Uuid TenantId, Uuid ApplicationId,
    long PlanRevision, string PlanSha256, ImmutableContentRef FrozenPlan,
    ApplicationImportPlannedRow Row, DateTimeOffset RecordedAt, OperationProvenance Operation,
    ImmutableContentRef Intent, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult,
    Uuid BatchId, long ResultingRevision) : DomainEvent, INativeOperationFact
{
    /// <summary>The system process that records the provisional effect.</summary>
    public ActorReference StoredActor { get; } = ActorReference.ForSystemProcess(
        "reactor:application-import", "application-import");
}
