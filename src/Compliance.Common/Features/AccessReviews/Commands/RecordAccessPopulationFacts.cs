using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>
///     Replaces a draft population's observed facts as one complete immutable set; an accepted population cannot
///     change. <paramref name="CommandId" /> is the logical operation identity and <paramref name="ExpectedRevision" />
///     is the population's logical revision.
/// </summary>
[Discriminator("bdgrz.access_population.facts.record", 2)]
public sealed record RecordAccessPopulationFacts(Uuid CommandId, Uuid TenantId, Uuid PopulationId,
    long ExpectedRevision, IReadOnlyList<AccessPrincipalFact> Principals,
    IReadOnlyList<AccessEntitlementFact> Entitlements,
    IReadOnlyList<AccessGroupMemberFact> GroupMembers,
    IReadOnlyList<AccessAssignmentFact> Assignments)
    : IRequest<AccessPopulationRegistration>, IAccessReviewMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
