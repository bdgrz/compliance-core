using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>
///     Opens a manually attested population draft for one exact system-instance revision.
///     <paramref name="PopulationId" /> is the immutable creation identity: an exact retry returns the original
///     registration even after later facts or acceptance, and the same id with a changed intent conflicts.
/// </summary>
[Discriminator("bdgrz.access_population.open", 2)]
public sealed record OpenAccessPopulation(Uuid PopulationId, Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long ExpectedSystemInstanceRevision, DateTimeOffset ObservedAt,
    string Source)
    : IRequest<AccessPopulationRegistration>, IAccessReviewMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => PopulationId;
}
