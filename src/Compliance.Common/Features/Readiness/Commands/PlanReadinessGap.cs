using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Commands;

/// <summary>
///     Assigns an owner, target date, and planned action to a gap's stable identity.
///     <paramref name="ExpectedRevision" /> is the revision of the gap plan, not of the program readiness. A gap plan
///     held by a readiness decision refuses the change until the decision settles.
/// </summary>
[Discriminator("bdgrz.readiness.gap.plan", 2)]
public sealed record PlanReadinessGap(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid GapId,
    long ExpectedRevision, Uuid OwnerMemberId, DateOnly TargetDate, string Action)
    : IRequest<ReadinessGapPlanView>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
