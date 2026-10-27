using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Records authored report facts; this asserts no coverage or approval. <paramref name="ReportId" /> is the
///     logical identity of the creation, so an exact retry replays the original acceptance.
/// </summary>
[Discriminator("bdgrz.provider.assurance_report.record", 1)]
public sealed record RecordAssuranceReport(Uuid ReportId, Uuid TenantId, Uuid ProviderId, AssuranceReportContent Content)
    : IRequest<AssuranceReportRegistration>, IProviderManagementRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => ReportId;
}
