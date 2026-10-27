using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Events;

/// <summary>
///     A governed workforce person became the risk owner. <paramref name="OwnerContent" /> is the complete
///     <see cref="RiskOwnerView" />.
/// </summary>
[Discriminator("bdgrz.risk.owner.assigned", 2)]
public sealed record RiskOwnerAssigned(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision,
    ImmutableContentRef OwnerContent, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
