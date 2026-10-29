using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Projections;

public interface IAdvisorFeedbackWorkItemProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
