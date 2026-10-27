using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Examinations.Commands;

/// <summary>Records the freeze type iiperiod close intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.freeze-type-iiperiod-close", 2)]
public sealed record FreezeTypeIIPeriodClose(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PeriodId, Uuid CloseSnapshotId, long ExpectedRevision) : IRequest<UnitResult>, IIdentifiedRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
