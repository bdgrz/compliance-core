using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Risks.Events;

/// <summary>
///     Treatment work was reported done. <paramref name="EvidenceRequestIdsManifest" /> is the complete ordered list of
///     the fulfilled evidence requests the submission rests on; the logical id of the operation is the submission id.
/// </summary>
[Discriminator("bdgrz.risk.treatment_action.completion_submitted", 2)]
public sealed record RiskTreatmentActionCompletionSubmitted(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision,
    Uuid ActionId, Uuid SubmissionId, string Summary, ImmutableContentRef EvidenceRequestIdsManifest,
    Uuid SubmitterMemberId, ActorReference SubmittedBy, DateTimeOffset SubmittedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
