using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Events;

/// <summary>
///     A control treatment assertion was proposed on the (risk, control) line. <paramref name="TreatmentContent" /> is
///     the complete proposed <see cref="RiskControlTreatmentView" />; the logical id of the operation is the treatment
///     id.
/// </summary>
[Discriminator("bdgrz.risk.control_treatment.proposed", 2)]
public sealed record RiskControlTreatmentProposed(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision,
    ImmutableContentRef TreatmentContent, Uuid ProposerMemberId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
