using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Appends authored report facts at the expected revision of the report owner; the provider cannot change.
///     <paramref name="CommandId" /> is the logical identity of the revision.
/// </summary>
[Discriminator("bdgrz.provider.assurance_report.revise", 1)]
public sealed record ReviseAssuranceReport(Uuid CommandId, Uuid TenantId, Uuid ProviderId, Uuid ReportId,
    long ExpectedRevision, AssuranceReportContent Content)
    : IRequest<AssuranceReportRegistration>, IProviderManagementRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
