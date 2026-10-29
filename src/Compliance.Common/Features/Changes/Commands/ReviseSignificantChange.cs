using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.Changes.Commands;

/// <summary>Records the revise significant change intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.changes.revise-significant-change", 3)]
public sealed record ReviseSignificantChange(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ChangeId, long ExpectedRevision, SignificantChangeContent Content, string ChangeReason) : IRequest<UnitResult>, IIdentifiedRequest, IRestrictedComplianceRecordMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
