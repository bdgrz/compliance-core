using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.Changes.Commands;

/// <summary>Records the record significant change intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.changes.record-significant-change", 3)]
public sealed record RecordSignificantChange(Uuid TenantId, Uuid ProgramId, Uuid ChangeId, SignificantChangeContent Content) : IRequest<UnitResult>, IIdentifiedRequest, IRestrictedComplianceRecordMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => ChangeId;
}
