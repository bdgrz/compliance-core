using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

public interface IAdvisorFeedbackWorkItemProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
