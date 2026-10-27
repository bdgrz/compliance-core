using Cntryl.Portia;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.AccessReviews.Contracts;

/// <summary>A tenant-scoped access-review request; it requires the access-review permission.</summary>
public interface IAccessReviewRequest : IRequestBase
{
    Uuid TenantId { get; }
}
