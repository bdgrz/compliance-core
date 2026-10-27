using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Commands;

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
