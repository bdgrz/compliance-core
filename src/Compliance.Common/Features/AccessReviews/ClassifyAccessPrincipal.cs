using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Explicitly classifies an accepted account; a new decision supersedes and preserves the prior one.
///     <paramref name="ExpectedRevision" /> is the number of decisions already recorded for this exact principal.
/// </summary>
[Discriminator("bdgrz.access_population.principal.classify", 2)]
public sealed record ClassifyAccessPrincipal(Uuid CommandId, Uuid TenantId, Uuid PopulationId,
    string ProviderSubjectId, long ExpectedRevision, string Classification,
    string Rationale, Uuid? PersonId = null, Uuid? ServiceIdentityId = null,
    Uuid? AccountableOwnerPersonId = null, string? SharedJustification = null)
    : IRequest<AccessPrincipalClassificationView>, IAccessReviewMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
