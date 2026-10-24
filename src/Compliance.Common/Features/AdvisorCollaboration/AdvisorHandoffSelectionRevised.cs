using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>
///     Replaces the selected versions of an open handoff and advances its selection generation.
///     <paramref name="RecordVersionsManifest" /> is the complete exact selection as verified content
///     (<c>AdvisorHandoffSelectionRevised.RecordVersionsManifest.v1</c>).
/// </summary>
[Discriminator("bdgrz.advisor-handoff.selection-revised", 1)]
public sealed record AdvisorHandoffSelectionRevised(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, long Revision,
    ImmutableContentRef RecordVersionsManifest, string ChangeReason, ActorReference RevisedBy,
    DateTimeOffset RevisedAt, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
