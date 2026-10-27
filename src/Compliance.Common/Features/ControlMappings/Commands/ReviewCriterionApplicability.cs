using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Accepts or rejects the pending not-applicable proposal named by <paramref name="DecisionId" /> at the exact
///     revision of its criterion line; HTTP-only. <paramref name="CommandId" /> is the logical operation identity of
///     the review. The proposer may review only under an approved waiver scoped to
///     (criterion_applicability, decision_id, decision_id, expected_revision, review).
/// </summary>
[Discriminator("bdgrz.criterion_applicability.review", 2)]
public sealed record ReviewCriterionApplicability(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid DecisionId,
    long ExpectedRevision, string Outcome, string Rationale, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
