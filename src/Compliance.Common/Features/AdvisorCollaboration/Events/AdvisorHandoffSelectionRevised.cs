using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Events;

/// <summary>
///     Replaces the selected versions of an open handoff and advances its selection generation.
///     <paramref name="RecordVersionsManifest" /> is the complete exact selection as verified content
///     (<c>AdvisorHandoffSelectionRevised.RecordVersionsManifest.v1</c>).
/// </summary>
[Discriminator("bdgrz.advisor-handoff.selection-revised", 2)]
public sealed record AdvisorHandoffSelectionRevised(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, long Revision,
    ImmutableContentRef RecordVersionsManifest, ImmutableContentRef Authored, ActorReference RevisedBy,
    DateTimeOffset RevisedAt, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
