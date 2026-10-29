using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessReviews.Projections;

public interface IAccessReviewCampaignImpactProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
