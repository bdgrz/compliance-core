using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Admits the owning client's personal management acknowledgement against the exact engagement content revision
///     and the exact service epoch, and acknowledges it with a fixed 202. <paramref name="AcknowledgementId" /> is the
///     logical operation identity. The acknowledgement is materialized later, only after the engagement owner
///     authorizes the personal decision; it never approves production acceptance.
/// </summary>
[Discriminator("bdgrz.service-engagement.management-acknowledge", 1)]
public sealed record AcknowledgeEngagementManagement(Uuid TenantId, Uuid EngagementId, Uuid AcknowledgementId,
    long ExpectedEngagementRevision, IReadOnlyList<Uuid> CompleteServiceRecordIds, string Statement,
    long ExpectedServiceEpoch)
    : IRequest<OperationAcceptedResponse>, IIndependenceAdministrationRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => AcknowledgementId;
}
