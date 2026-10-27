using Cntryl.Portia;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Commands;

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
