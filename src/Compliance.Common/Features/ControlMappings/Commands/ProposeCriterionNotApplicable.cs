using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings.Commands;

/// <summary>
///     Proposes that one criterion of the program's selected edition does not apply. It counts only after an
///     independent review accepts it; HTTP-only. <paramref name="DecisionId" /> identifies this immutable proposal and
///     is the logical operation identity, so an exact retry returns the original registration even after the
///     proposal was reviewed. <paramref name="ExpectedRevision" /> is the revision of the criterion's applicability
///     line: 0 for the first proposal and the current revision for a later one.
/// </summary>
[Discriminator("bdgrz.criterion_applicability.propose_not_applicable", 2)]
public sealed record ProposeCriterionNotApplicable(Uuid DecisionId, Uuid TenantId, Uuid ProgramId, Uuid EditionId,
    string CriterionIdentifier, long ExpectedRevision, string Rationale)
    : IRequest<CriterionApplicabilityRegistration>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => DecisionId;
}
