using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Records the revise significant change intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.changes.revise-significant-change", 3)]
public sealed record ReviseSignificantChange(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ChangeId, long ExpectedRevision, SignificantChangeContent Content, string ChangeReason) : IRequest<UnitResult>, IIdentifiedRequest, IRestrictedComplianceRecordMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
