using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Records the assess significant change intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.changes.assess-significant-change", 3)]
public sealed record AssessSignificantChange(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ChangeId, long ExpectedRevision, SignificantChangeImpactContent Content) : IRequest<UnitResult>, IIdentifiedRequest, IRestrictedComplianceRecordMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
