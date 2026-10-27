using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the external disposition of the exact delivered response version; it is immutable once recorded.</summary>
[Discriminator("bdgrz.auditor-requests.record-auditor-response-disposition", 2)]
public sealed record RecordAuditorResponseDisposition(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RequestId,
    Uuid ResponseId, long Version, string Disposition, string? ExternalReference, long ExpectedRevision)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
