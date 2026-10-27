using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.ControlMappings.Commands;

/// <summary>
///     Restores the accepted not-applicable criterion named by <paramref name="DecisionId" /> to coverage, keeping
///     its history. A decision that a newer accepted proposal superseded cannot be withdrawn.
///     <paramref name="CommandId" /> is the logical operation identity.
/// </summary>
[Discriminator("bdgrz.criterion_applicability.withdraw_not_applicable", 2)]
public sealed record WithdrawCriterionNotApplicable(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid DecisionId,
    long ExpectedRevision, string Rationale)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
