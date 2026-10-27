using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Appends one nonattest service to the complete history of a client. <paramref name="ServiceRecordId" /> is the
///     logical operation identity, so an exact retry replays the original view. <paramref name="ExpectedServiceEpoch" />
///     is the current number of recorded services of that client; evaluations never advance it.
/// </summary>
[Discriminator("bdgrz.independence.service.record", 1)]
public sealed record RecordNonattestService(Uuid TenantId, Uuid ServiceRecordId,
    long ExpectedServiceEpoch, NonattestServiceContent Content) : IRequest<NonattestServiceView>,
    IIndependenceAdministrationRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => ServiceRecordId;
}
