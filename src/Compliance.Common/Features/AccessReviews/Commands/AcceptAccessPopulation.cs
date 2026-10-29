using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>
///     The reviewer's personal attestation that the draft is the observed population. It freezes
///     an immutable snapshot. HTTP-only; it is not registered as an MCP tool. <paramref name="CommandId" /> is the
///     logical operation identity and the snapshot identity.
/// </summary>
[Discriminator("bdgrz.access_population.accept", 2)]
public sealed record AcceptAccessPopulation(Uuid CommandId, Uuid TenantId, Uuid PopulationId,
    long ExpectedRevision, string Attestation)
    : IRequest<AccessPopulationAcceptance>, IAccessReviewMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
