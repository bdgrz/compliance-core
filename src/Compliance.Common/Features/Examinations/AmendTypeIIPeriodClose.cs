using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the amend type iiperiod close intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.amend-type-iiperiod-close", 2)]
public sealed record AmendTypeIIPeriodClose(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PeriodId, Uuid CloseSnapshotId, long ExpectedRevision, string AmendmentReason) : IRequest<UnitResult>, IIdentifiedRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
