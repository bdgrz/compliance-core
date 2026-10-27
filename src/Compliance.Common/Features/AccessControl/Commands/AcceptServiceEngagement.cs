using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Admits the acceptance of one service engagement by its designated partner against the exact engagement revision
///     and acknowledges it with a fixed 202. <paramref name="CommandId" /> is the logical operation identity. Admission
///     freezes the complete service epoch, the paired personal decisions, the rules, the partner authority and the
///     permanent practice subjects; the engagement becomes accepted only after the ordered acceptance process
///     authorizes every temporary grant irreversibly and commits the permanent practice walls.
/// </summary>
[Discriminator("bdgrz.service-engagement.accept", 1)]
public sealed record AcceptServiceEngagement(Uuid CommandId, Uuid TenantId, Uuid EngagementId, long ExpectedRevision)
    : IRequest<OperationAcceptedResponse>, IServiceEngagementAcceptanceRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
