using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.Changes.Commands;

/// <summary>Records the assess significant change intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.changes.assess-significant-change", 3)]
public sealed record AssessSignificantChange(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ChangeId, long ExpectedRevision, SignificantChangeImpactContent Content) : IRequest<UnitResult>, IIdentifiedRequest, IRestrictedComplianceRecordMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
