using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Examinations.Commands;

/// <summary>Records the approve type iiperiod close intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.approve-type-iiperiod-close", 3)]
public sealed record ApproveTypeIIPeriodClose(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PeriodId,
    long ExpectedRevision, string Rationale, IReadOnlyList<Uuid> AcknowledgedMaterialItemIds)
    : IRequest<UnitResult>, IIdentifiedRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
