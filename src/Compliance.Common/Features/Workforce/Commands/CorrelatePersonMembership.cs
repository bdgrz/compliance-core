using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Correlates a roster person with the tenant platform member identified by <c>UserId</c>, or
///     clears the correlation when <c>UserId</c> is null. Correlation never grants platform access;
///     a member correlated with several people surfaces as a <c>duplicate</c> reconciliation
///     observation instead of being rejected.
/// </summary>
[Discriminator("bdgrz.workforce.person.correlate-membership", 2)]
public sealed record CorrelatePersonMembership(Uuid CommandId, Uuid TenantId, Uuid PersonId,
    long ExpectedRevision, Uuid? UserId = null) : IRequest<UnitResult>, IIdentifiedRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
