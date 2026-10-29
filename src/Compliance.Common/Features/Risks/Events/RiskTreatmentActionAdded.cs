using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Risks.Events;

/// <summary>
///     Accountable treatment work was added to the risk. <paramref name="ActionContent" /> is the complete open
///     <see cref="RiskTreatmentActionView" />; the logical id of the operation is the action id and the revision is one.
/// </summary>
[Discriminator("bdgrz.risk.treatment_action.added", 2)]
public sealed record RiskTreatmentActionAdded(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision,
    ImmutableContentRef ActionContent, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
