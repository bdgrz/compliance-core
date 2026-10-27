using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Records the record significant change intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.changes.record-significant-change", 3)]
public sealed record RecordSignificantChange(Uuid TenantId, Uuid ProgramId, Uuid ChangeId, SignificantChangeContent Content) : IRequest<UnitResult>, IIdentifiedRequest, IRestrictedComplianceRecordMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => ChangeId;
}
