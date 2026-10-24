using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Cancels a pending, not yet applied amendment. The period fences a delayed apply atomically with the
///     cancellation, and only afterwards is the pending calendar reservation released. An applied amendment is
///     irreversible and returns a conflict.
/// </summary>
[Discriminator("bdgrz.operations.cancel-type-ii-period-amendment", 1)]
public sealed record CancelTypeIIPeriodAmendment(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PeriodId,
    Uuid AmendmentId, long ExpectedRevision, string Reason)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
