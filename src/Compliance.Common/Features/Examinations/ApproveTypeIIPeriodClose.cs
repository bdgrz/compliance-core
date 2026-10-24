using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the approve type iiperiod close intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.approve-type-iiperiod-close", 3)]
public sealed record ApproveTypeIIPeriodClose(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PeriodId,
    long ExpectedRevision, string Rationale, IReadOnlyList<Uuid> AcknowledgedMaterialItemIds)
    : IRequest<UnitResult>, IIdentifiedRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
