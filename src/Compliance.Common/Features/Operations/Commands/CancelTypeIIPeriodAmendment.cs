using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Readiness.Models;
namespace Bdgrz.Compliance.Features.Operations.Commands;

/// <summary>
///     Cancels a pending, not yet applied amendment. The period fences a delayed apply atomically with the
///     cancellation, and only afterwards is the pending calendar reservation released. An applied amendment is
///     irreversible and returns a conflict.
/// </summary>
[Discriminator("bdgrz.operations.cancel-type-ii-period-amendment", 1)]
public sealed record CancelTypeIIPeriodAmendment(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PeriodId,
    Uuid AmendmentId, long ExpectedRevision, string Reason)
    : IRequest<UnitResult>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
