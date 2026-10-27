using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Events;

/// <summary>
///     Open treatment work was revised. <paramref name="EvidenceRequestIdsManifest" /> is the complete ordered list of
///     cited evidence request ids.
/// </summary>
[Discriminator("bdgrz.risk.treatment_action.revised", 2)]
public sealed record RiskTreatmentActionRevised(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision,
    Uuid ActionId, Uuid RequestId, long ExpectedRevision, string Title, string TargetState,
    string ExpectedEvidence, DateOnly DueOn, Uuid AccountableMemberId, ImmutableContentRef EvidenceRequestIdsManifest,
    ActorReference RevisedBy, DateTimeOffset RevisedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
